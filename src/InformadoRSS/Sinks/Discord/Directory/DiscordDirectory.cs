using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace InformadoRSS.Sinks.Discord;

/// <summary>
/// Lists the guilds and channels the bot can see and checks its permissions in a channel, for the destination picker.
/// Discord responses are kept in <see cref="DiscordDirectoryCache"/>; pass <c>refresh</c> to ask Discord again.
/// </summary>
public sealed class DiscordDirectory(
    DiscordRestClient client,
    IDiscordTokenProvider tokenProvider,
    DiscordDirectoryCache cache,
    TimeProvider time)
{
    public const string TextType = "text";
    public const string AnnouncementType = "announcement";

    private const string CdnBase = "https://cdn.discordapp.com/";

    public async Task<DiscordResult<IReadOnlyList<DiscordGuildSummary>>> GetGuildsAsync(bool refresh, CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            return new(null, MissingToken());
        }

        var guilds = await CachedAsync(token, "guilds", refresh, () => FetchGuildsAsync(cancellationToken));
        if (!guilds.IsSuccess)
        {
            return new(null, guilds.Failure);
        }

        return new(
            guilds.Value
                .OrderBy(guild => guild.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(guild => guild.Id, SnowflakeComparer.Instance)
                .Select(guild => new DiscordGuildSummary(guild.Id, guild.Name, IconUrl(guild)))
                .ToList(),
            null);
    }

    /// <summary>
    /// Lists the text and announcement channels of a guild, in the order Discord shows them, with whether the bot can
    /// post in each. Failing to read the bot's permissions leaves <see cref="DiscordChannelSummary.CanPost"/> unknown
    /// instead of failing the listing.
    /// </summary>
    public async Task<DiscordResult<IReadOnlyList<DiscordChannelSummary>>> GetChannelsAsync(
        string guildId,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            return new(null, MissingToken());
        }

        var channels = await GetGuildChannelsAsync(token, guildId, refresh, cancellationToken);
        if (!channels.IsSuccess)
        {
            return new(null, channels.Failure);
        }

        var membership = await GetMembershipAsync(token, guildId, refresh, cancellationToken);
        var now = time.GetUtcNow();
        var categories = channels.Value
            .Where(channel => channel.Type == (int)DiscordChannelType.Category)
            .ToDictionary(channel => channel.Id, StringComparer.Ordinal);

        return new(
            channels.Value
                .Where(channel => TypeName(channel) is not null)
                .Select(channel =>
                {
                    var category = channel.ParentId is not null && categories.TryGetValue(channel.ParentId, out var parent) ? parent : null;
                    return (Channel: channel, Category: category);
                })
                .OrderBy(entry => entry.Category is null ? 0 : 1)
                .ThenBy(entry => entry.Category?.Position ?? 0)
                .ThenBy(entry => entry.Category?.Id, SnowflakeComparer.Instance)
                .ThenBy(entry => entry.Channel.Position)
                .ThenBy(entry => entry.Channel.Id, SnowflakeComparer.Instance)
                .Select(entry => new DiscordChannelSummary(
                    entry.Channel.Id,
                    entry.Channel.Name ?? entry.Channel.Id,
                    TypeName(entry.Channel)!,
                    entry.Category?.Id,
                    entry.Category?.Name,
                    entry.Channel.Position,
                    membership.IsSuccess ? CanPost(Permissions(membership.Value, entry.Channel, now)) : null))
                .ToList(),
            null);
    }

    /// <summary>
    /// Computes the bot's permissions in a text or announcement channel of a guild. A channel that is not in the
    /// guild, or is of another type, fails with a 404 <see cref="DiscordApiResponse"/>.
    /// </summary>
    public async Task<DiscordResult<DiscordChannelPermissions>> CheckPermissionsAsync(
        string guildId,
        string channelId,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var token = await tokenProvider.GetTokenAsync(cancellationToken);
        if (token is null)
        {
            return new(null, MissingToken());
        }

        var channels = await GetGuildChannelsAsync(token, guildId, refresh, cancellationToken);
        if (!channels.IsSuccess)
        {
            return new(null, channels.Failure);
        }

        var channel = channels.Value.FirstOrDefault(candidate => string.Equals(candidate.Id, channelId, StringComparison.Ordinal));
        var type = channel is null ? null : TypeName(channel);
        if (channel is null || type is null)
        {
            return new(
                null,
                new DiscordApiResponse(
                    DiscordApiOutcome.HttpError,
                    StatusCode: (int)HttpStatusCode.NotFound,
                    Message: $"channel {channelId} is not a text or announcement channel the bot can see in this server"));
        }

        var membership = await GetMembershipAsync(token, guildId, refresh, cancellationToken);
        if (!membership.IsSuccess)
        {
            return new(null, membership.Failure);
        }

        var now = time.GetUtcNow();
        var permissions = Permissions(membership.Value, channel, now);
        var checks = RequiredPermissions
            .Select(required => new DiscordPermissionCheck(required.Name, required.Label, (permissions & required.Bit) == required.Bit))
            .ToList();
        var timedOutUntil = DiscordPermissionCalculator.IsTimedOut(membership.Value.Member, now)
            ? membership.Value.Member.CommunicationDisabledUntil
            : null;
        return new(
            new DiscordChannelPermissions(guildId, channelId, type, checks.TrueForAll(check => check.Granted), checks, timedOutUntil),
            null);
    }

    private static readonly (string Name, string Label, ulong Bit)[] RequiredPermissions =
    [
        ("viewChannel", "View Channel", DiscordPermissions.ViewChannel),
        ("sendMessages", "Send Messages", DiscordPermissions.SendMessages),
        ("embedLinks", "Embed Links", DiscordPermissions.EmbedLinks),
    ];

    private static ulong Permissions(DiscordBotMembership membership, DiscordChannel channel, DateTimeOffset now)
    {
        var permissions = DiscordPermissionCalculator.Compute(membership.Guild, membership.Member, membership.UserId, channel);
        return DiscordPermissionCalculator.IsTimedOut(membership.Member, now)
            ? DiscordPermissionCalculator.ApplyTimeout(permissions)
            : permissions;
    }

    private static bool CanPost(ulong permissions) =>
        Array.TrueForAll(RequiredPermissions, required => (permissions & required.Bit) == required.Bit);

    private async Task<DiscordResult<DiscordBotMembership>> GetMembershipAsync(
        string token,
        string guildId,
        bool refresh,
        CancellationToken cancellationToken)
    {
        var user = await CachedAsync(
            token,
            "user",
            refresh: false,
            () => ReadAsync(client.GetCurrentUserAsync(cancellationToken), DiscordJsonContext.Default.DiscordUser));
        if (!user.IsSuccess)
        {
            return new(null, user.Failure);
        }

        var guild = await CachedAsync(
            token,
            $"guild:{guildId}",
            refresh,
            () => ReadAsync(client.GetGuildAsync(guildId, cancellationToken), DiscordJsonContext.Default.DiscordGuild));
        if (!guild.IsSuccess)
        {
            return new(null, guild.Failure);
        }

        var member = await CachedAsync(
            token,
            $"member:{guildId}",
            refresh,
            () => ReadAsync(client.GetGuildMemberAsync(guildId, user.Value.Id, cancellationToken), DiscordJsonContext.Default.DiscordGuildMember));
        if (!member.IsSuccess)
        {
            return new(null, member.Failure);
        }

        return new(new DiscordBotMembership(guild.Value, member.Value, user.Value.Id), null);
    }

    private Task<DiscordResult<List<DiscordChannel>>> GetGuildChannelsAsync(
        string token,
        string guildId,
        bool refresh,
        CancellationToken cancellationToken) =>
        CachedAsync(
            token,
            $"channels:{guildId}",
            refresh,
            () => ReadAsync(client.GetGuildChannelsAsync(guildId, cancellationToken), DiscordJsonContext.Default.ListDiscordChannel));

    private async Task<DiscordResult<List<DiscordPartialGuild>>> FetchGuildsAsync(CancellationToken cancellationToken)
    {
        List<DiscordPartialGuild> guilds = [];
        string? after = null;
        while (true)
        {
            var page = await ReadAsync(
                client.GetCurrentUserGuildsAsync(after, DiscordRestClient.MaxGuildsPerPage, cancellationToken),
                DiscordJsonContext.Default.ListDiscordPartialGuild);
            if (!page.IsSuccess)
            {
                return page;
            }

            guilds.AddRange(page.Value);
            if (page.Value.Count < DiscordRestClient.MaxGuildsPerPage)
            {
                return new(guilds, null);
            }

            after = page.Value.Select(guild => guild.Id).Max(SnowflakeComparer.Instance);
        }
    }

    private async Task<DiscordResult<T>> CachedAsync<T>(string token, string key, bool refresh, Func<Task<DiscordResult<T>>> fetch)
        where T : class
    {
        if (!refresh && cache.TryGet(token, key, out T? cached))
        {
            return new(cached, null);
        }

        var result = await fetch();
        if (result.IsSuccess)
        {
            cache.Set(token, key, result.Value);
        }

        return result;
    }

    private static async Task<DiscordResult<T>> ReadAsync<T>(Task<DiscordApiResponse> request, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        var response = await request;
        if (!response.IsSuccess)
        {
            return new(null, response);
        }

        T? value;
        try
        {
            value = JsonSerializer.Deserialize(response.Body ?? string.Empty, typeInfo);
        }
        catch (JsonException)
        {
            value = null;
        }

        return value is null
            ? new(null, new DiscordApiResponse(DiscordApiOutcome.HttpError, StatusCode: response.StatusCode, Message: "unexpected response from Discord"))
            : new(value, null);
    }

    private static DiscordApiResponse MissingToken() =>
        new(DiscordApiOutcome.MissingToken, Message: "Discord bot token not configured");

    private static string? TypeName(DiscordChannel channel) =>
        (DiscordChannelType)channel.Type switch
        {
            DiscordChannelType.Text => TextType,
            DiscordChannelType.Announcement => AnnouncementType,
            _ => null,
        };

    private static string? IconUrl(DiscordPartialGuild guild) =>
        string.IsNullOrEmpty(guild.Icon)
            ? null
            : $"{CdnBase}icons/{guild.Id}/{guild.Icon}.{(guild.Icon.StartsWith("a_", StringComparison.Ordinal) ? "gif" : "png")}";
}
