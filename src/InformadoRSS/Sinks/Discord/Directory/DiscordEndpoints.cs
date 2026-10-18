using System.Net;
using System.Text.Json;
using InformadoRSS.Core;
using InformadoRSS.Templates;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace InformadoRSS.Sinks.Discord;

public static class DiscordEndpoints
{
    public const string TestItemTitle = "InformadoRSS test message";
    public const string TestItemLink = "https://github.com/christt105/InformadoRSS";

    /// <summary>
    /// Maps the destination picker endpoints under <c>/api/discord</c>: guilds, channels, the bot's permissions in a
    /// channel and a test message. Requires <see cref="DiscordDirectoryServiceCollectionExtensions.AddDiscordDirectory"/>.
    /// </summary>
    public static IEndpointRouteBuilder MapDiscordApi(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/discord");
        group.MapGet("/guilds", GetGuildsAsync);
        group.MapGet("/guilds/{guildId}/channels", GetChannelsAsync);
        group.MapGet("/guilds/{guildId}/channels/{channelId}/permissions", GetPermissionsAsync);
        group.MapPost("/channels/{channelId}/test-message", SendTestMessageAsync);
        return endpoints;
    }

    private static async Task<IResult> GetGuildsAsync(
        [FromServices] DiscordDirectory directory,
        [FromQuery] bool? refresh,
        CancellationToken cancellationToken)
    {
        var result = await directory.GetGuildsAsync(refresh == true, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Json(result.Value, AppJsonContext.Default.IReadOnlyListDiscordGuildSummary)
            : Failure(result.Failure);
    }

    private static async Task<IResult> GetChannelsAsync(
        [FromRoute] string guildId,
        [FromServices] DiscordDirectory directory,
        [FromQuery] bool? refresh,
        CancellationToken cancellationToken)
    {
        if (!SnowflakeComparer.IsSnowflake(guildId))
        {
            return InvalidId("guildId");
        }

        var result = await directory.GetChannelsAsync(guildId, refresh == true, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Json(result.Value, AppJsonContext.Default.IReadOnlyListDiscordChannelSummary)
            : Failure(result.Failure);
    }

    private static async Task<IResult> GetPermissionsAsync(
        [FromRoute] string guildId,
        [FromRoute] string channelId,
        [FromServices] DiscordDirectory directory,
        [FromQuery] bool? refresh,
        CancellationToken cancellationToken)
    {
        if (!SnowflakeComparer.IsSnowflake(guildId))
        {
            return InvalidId("guildId");
        }

        if (!SnowflakeComparer.IsSnowflake(channelId))
        {
            return InvalidId("channelId");
        }

        var result = await directory.CheckPermissionsAsync(guildId, channelId, refresh == true, cancellationToken);
        return result.IsSuccess
            ? TypedResults.Json(result.Value, AppJsonContext.Default.DiscordChannelPermissions)
            : Failure(result.Failure);
    }

    private static async Task<IResult> SendTestMessageAsync(
        [FromRoute] string channelId,
        HttpRequest httpRequest,
        [FromServices] DiscordRestClient client,
        [FromServices] IMessageRenderer renderer,
        [FromServices] TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (!SnowflakeComparer.IsSnowflake(channelId))
        {
            return InvalidId("channelId");
        }

        DiscordTestMessageRequest? request = null;
        if (httpRequest.HasJsonContentType() && httpRequest.ContentLength is not 0)
        {
            try
            {
                request = await httpRequest.ReadFromJsonAsync(AppJsonContext.Default.DiscordTestMessageRequest, cancellationToken);
            }
            catch (JsonException)
            {
                return Error(
                    HttpStatusCode.BadRequest,
                    new DiscordEndpointError("invalid_body", "the body must be a JSON object such as {\"templateJson\":null}"));
            }
        }

        var templateJson = request?.TemplateJson;
        var template = TemplateJson.Parse(templateJson);
        if (!template.IsValid)
        {
            return Error(
                HttpStatusCode.BadRequest,
                new DiscordEndpointError("invalid_template", "the template is not valid", TemplateErrors: template.Errors));
        }

        var message = renderer.Render(templateJson, TestItem(timeProvider.GetUtcNow()));
        var response = await client.CreateMessageAsync(channelId, DiscordSink.ToPayload(message), cancellationToken);
        if (!response.IsSuccess)
        {
            return Failure(response);
        }

        return TypedResults.Json(
            new DiscordTestMessageResponse(channelId, ReadMessageId(response.Body)),
            AppJsonContext.Default.DiscordTestMessageResponse);
    }

    private static FeedItem TestItem(DateTimeOffset now) =>
        new(
            Id: "informadorss-test-message",
            Title: TestItemTitle,
            Link: TestItemLink,
            DescriptionHtml: "<p>If you can read this, InformadoRSS can post to this channel.</p>",
            DescriptionText: "If you can read this, InformadoRSS can post to this channel.",
            Author: "InformadoRSS",
            Published: now,
            Categories: [],
            ImageUrl: null);

    private static string? ReadMessageId(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(body, DiscordJsonContext.Default.DiscordMessage)?.Id;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static JsonHttpResult<DiscordEndpointError> InvalidId(string name) =>
        Error(HttpStatusCode.BadRequest, new DiscordEndpointError("invalid_id", $"{name} must be a numeric Discord id"));

    private static JsonHttpResult<DiscordEndpointError> Failure(DiscordApiResponse response)
    {
        var detail = Describe(response);
        return response.Outcome switch
        {
            DiscordApiOutcome.MissingToken => Error(
                HttpStatusCode.Conflict,
                new DiscordEndpointError("token_missing", "no Discord bot token is configured")),
            DiscordApiOutcome.RateLimited => Error(
                HttpStatusCode.TooManyRequests,
                new DiscordEndpointError("rate_limited", "rate limited by Discord, try again later", response.RetryAfter?.TotalSeconds)),
            DiscordApiOutcome.NetworkError => Error(
                HttpStatusCode.BadGateway,
                new DiscordEndpointError("discord_unavailable", $"could not reach Discord ({detail})")),
            _ => response.StatusCode switch
            {
                (int)HttpStatusCode.Unauthorized => Error(
                    HttpStatusCode.BadGateway,
                    new DiscordEndpointError("token_invalid", $"Discord rejected the bot token, check it in the settings ({detail})")),
                (int)HttpStatusCode.Forbidden => Error(
                    HttpStatusCode.Forbidden,
                    new DiscordEndpointError("forbidden", $"the bot has no access to this server or channel ({detail})")),
                (int)HttpStatusCode.NotFound => Error(
                    HttpStatusCode.NotFound,
                    new DiscordEndpointError("not_found", $"Discord does not know this server or channel, or the bot is not in it ({detail})")),
                null or >= 500 => Error(
                    HttpStatusCode.BadGateway,
                    new DiscordEndpointError("discord_unavailable", $"Discord is unavailable ({detail})")),
                _ => Error(HttpStatusCode.BadGateway, new DiscordEndpointError("discord_error", $"Discord answered with an error ({detail})")),
            },
        };
    }

    private static JsonHttpResult<DiscordEndpointError> Error(HttpStatusCode status, DiscordEndpointError error) =>
        TypedResults.Json(error, AppJsonContext.Default.DiscordEndpointError, statusCode: (int)status);

    private static string Describe(DiscordApiResponse response)
    {
        var parts = new List<string>(3);
        if (response.StatusCode is { } status)
        {
            parts.Add($"HTTP {status}");
        }

        if (response.ErrorCode is { } code and not 0)
        {
            parts.Add($"code {code}");
        }

        if (!string.IsNullOrWhiteSpace(response.Message))
        {
            parts.Add(response.Message);
        }

        return string.Join(", ", parts);
    }
}
