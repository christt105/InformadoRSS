using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace InformadoRSS.Sinks.Discord;

/// <summary>
/// Minimal Discord REST API v10 client authenticated as a bot. Waits for and retries rate limits within
/// <see cref="DiscordClientOptions"/>; never logs or returns the token.
/// </summary>
public sealed class DiscordRestClient(
    HttpClient httpClient,
    IDiscordTokenProvider tokenProvider,
    DiscordRateLimiter rateLimiter,
    DiscordClientOptions options,
    TimeProvider timeProvider)
{
    public static Uri BaseUri { get; } = new("https://discord.com/api/v10/");

    public static string UserAgent { get; } = $"DiscordBot (https://github.com/christt105/InformadoRSS, {AppInfo.Version})";

    /// <summary>The largest page <see cref="GetCurrentUserGuildsAsync"/> accepts.</summary>
    public const int MaxGuildsPerPage = 200;

    /// <summary>Posts a message to a channel (<c>POST /channels/{channel.id}/messages</c>).</summary>
    public Task<DiscordApiResponse> CreateMessageAsync(string channelId, DiscordMessagePayload payload, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(channelId);
        ArgumentNullException.ThrowIfNull(payload);

        var uri = new Uri(BaseUri, $"channels/{Uri.EscapeDataString(channelId)}/messages");
        return SendAsync(
            $"POST channels/{{channel.id}}/messages:{channelId}",
            () => new HttpRequestMessage(HttpMethod.Post, uri)
            {
                Content = JsonContent.Create(payload, DiscordJsonContext.Default.DiscordMessagePayload),
            },
            cancellationToken);
    }

    /// <summary>Gets the bot's own user (<c>GET /users/@me</c>).</summary>
    public Task<DiscordApiResponse> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        GetAsync("users/@me", "GET users/@me", cancellationToken);

    /// <summary>
    /// Gets one page of the bot's guilds (<c>GET /users/@me/guilds</c>), sorted by id, starting after <paramref name="after"/>.
    /// </summary>
    public Task<DiscordApiResponse> GetCurrentUserGuildsAsync(string? after, int limit, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(limit, MaxGuildsPerPage);

        var query = after is null
            ? $"?limit={limit.ToString(CultureInfo.InvariantCulture)}"
            : $"?limit={limit.ToString(CultureInfo.InvariantCulture)}&after={Uri.EscapeDataString(after)}";
        return GetAsync($"users/@me/guilds{query}", "GET users/@me/guilds", cancellationToken);
    }

    /// <summary>Gets a guild, including its roles and owner (<c>GET /guilds/{guild.id}</c>).</summary>
    public Task<DiscordApiResponse> GetGuildAsync(string guildId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guildId);
        return GetAsync($"guilds/{Uri.EscapeDataString(guildId)}", $"GET guilds/{{guild.id}}:{guildId}", cancellationToken);
    }

    /// <summary>Lists a guild's channels, without threads (<c>GET /guilds/{guild.id}/channels</c>).</summary>
    public Task<DiscordApiResponse> GetGuildChannelsAsync(string guildId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guildId);
        return GetAsync(
            $"guilds/{Uri.EscapeDataString(guildId)}/channels",
            $"GET guilds/{{guild.id}}/channels:{guildId}",
            cancellationToken);
    }

    /// <summary>Gets a member of a guild (<c>GET /guilds/{guild.id}/members/{user.id}</c>).</summary>
    public Task<DiscordApiResponse> GetGuildMemberAsync(string guildId, string userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(guildId);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        return GetAsync(
            $"guilds/{Uri.EscapeDataString(guildId)}/members/{Uri.EscapeDataString(userId)}",
            $"GET guilds/{{guild.id}}/members/{{user.id}}:{guildId}",
            cancellationToken);
    }

    private Task<DiscordApiResponse> GetAsync(string path, string bucket, CancellationToken cancellationToken)
    {
        var uri = new Uri(BaseUri, path);
        return SendAsync(bucket, () => new HttpRequestMessage(HttpMethod.Get, uri), cancellationToken);
    }

    private async Task<DiscordApiResponse> SendAsync(
        string bucket,
        Func<HttpRequestMessage> createRequest,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            return new DiscordApiResponse(DiscordApiOutcome.MissingToken, Message: "Discord bot token not configured");
        }

        for (var attempt = 0; ; attempt++)
        {
            var delay = rateLimiter.GetDelay(bucket);
            if (delay > TimeSpan.Zero)
            {
                if (delay > options.MaxWait)
                {
                    return RateLimited(delay);
                }

                await Task.Delay(delay, timeProvider, cancellationToken);
            }

            using var request = createRequest();
            request.Headers.Authorization = new AuthenticationHeaderValue("Bot", token);
            request.Headers.TryAddWithoutValidation("User-Agent", UserAgent);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(options.RequestTimeout);

            HttpResponseMessage response;
            string body;
            try
            {
                response = await httpClient.SendAsync(request, timeout.Token);
                body = await response.Content.ReadAsStringAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return new DiscordApiResponse(DiscordApiOutcome.NetworkError, Message: "request to Discord timed out");
            }
            catch (HttpRequestException ex)
            {
                return new DiscordApiResponse(DiscordApiOutcome.NetworkError, Message: $"could not reach Discord: {ex.Message}");
            }

            using (response)
            {
                rateLimiter.Update(bucket, response.Headers);

                if (response.IsSuccessStatusCode)
                {
                    return new DiscordApiResponse(DiscordApiOutcome.Success, StatusCode: (int)response.StatusCode, Body: body);
                }

                var error = ReadError(body);
                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryAfter = ReadRetryAfter(error, response);
                    var global = error?.Global == true
                        || (response.Headers.TryGetValues("X-RateLimit-Global", out var globalValues)
                            && globalValues.Any(value => value.Equals("true", StringComparison.OrdinalIgnoreCase)));
                    rateLimiter.Block(bucket, retryAfter, global);
                    if (attempt >= options.MaxRetries || retryAfter > options.MaxWait)
                    {
                        return RateLimited(retryAfter);
                    }

                    continue;
                }

                return new DiscordApiResponse(
                    DiscordApiOutcome.HttpError,
                    StatusCode: (int)response.StatusCode,
                    ErrorCode: error?.Code,
                    Message: error?.Message ?? response.ReasonPhrase);
            }
        }
    }

    private static DiscordApiResponse RateLimited(TimeSpan retryAfter) =>
        new(
            DiscordApiOutcome.RateLimited,
            StatusCode: (int)HttpStatusCode.TooManyRequests,
            Message: "rate limited by Discord",
            RetryAfter: retryAfter);

    private static DiscordErrorBody? ReadError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(body, DiscordJsonContext.Default.DiscordErrorBody);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TimeSpan ReadRetryAfter(DiscordErrorBody? error, HttpResponseMessage response)
    {
        if (error?.RetryAfter is > 0 and var seconds)
        {
            return TimeSpan.FromSeconds(seconds);
        }

        if (response.Headers.RetryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (DiscordRateLimiter.TryReadNumber(response.Headers, "X-RateLimit-Reset-After", out var resetAfter) && resetAfter > 0)
        {
            return TimeSpan.FromSeconds(resetAfter);
        }

        return TimeSpan.FromSeconds(1);
    }
}
