using System.Text.Json;
using InformadoRSS.Core;
using InformadoRSS.Filters;
using InformadoRSS.Sinks.Discord;
using InformadoRSS.Sinks.Ntfy;
using InformadoRSS.Storage;
using InformadoRSS.Templates;

namespace InformadoRSS.Api;

/// <summary>
/// Validates route request bodies with the filter and template validators plus destination checks, and turns them into
/// <see cref="RouteSettings"/> with the filter and template stored in their normalized JSON form.
/// </summary>
public static class RouteInput
{
    public const int MaxNameLength = 200;
    public const int MaxDestinations = 25;

    /// <param name="sinks">Names of the registered sinks a destination may use.</param>
    /// <returns>Every problem found; when valid, <paramref name="settings"/> holds the route to store.</returns>
    public static RouteValidationResponse Validate(RouteRequest request, IReadOnlySet<string> sinks, out RouteSettings? settings)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sinks);

        settings = null;
        var errors = new List<ApiFieldError>();

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            errors.Add(new ApiFieldError("$.name", "the name is required"));
        }
        else if (name.Length > MaxNameLength)
        {
            errors.Add(new ApiFieldError("$.name", $"the name must be at most {MaxNameLength} characters"));
        }

        var filter = FilterJson.Parse(RawJson(request.Filter));
        var template = TemplateJson.Parse(RawJson(request.Template));
        var enabled = request.Enabled ?? true;
        var destinations = ValidateDestinations(request.Destinations ?? [], enabled, sinks, errors);

        var valid = errors.Count == 0 && filter.IsValid && template.IsValid;
        if (valid)
        {
            settings = new RouteSettings(
                name!,
                filter.Root is null ? null : FilterJson.Serialize(filter.Root),
                template.Template is null ? null : TemplateJson.Serialize(template.Template),
                enabled,
                destinations);
        }

        return new RouteValidationResponse(valid, errors, filter.Errors, template.Errors);
    }

    private static List<Destination> ValidateDestinations(
        IReadOnlyList<DestinationRequest?> requests, bool enabled, IReadOnlySet<string> sinks, List<ApiFieldError> errors)
    {
        var destinations = new List<Destination>(requests.Count);
        if (requests.Count == 0 && enabled)
        {
            errors.Add(new ApiFieldError("$.destinations", "an enabled route needs at least one destination"));
        }

        if (requests.Count > MaxDestinations)
        {
            errors.Add(new ApiFieldError("$.destinations", $"a route can have at most {MaxDestinations} destinations"));
            return destinations;
        }

        for (var i = 0; i < requests.Count; i++)
        {
            var path = $"$.destinations[{i}]";
            if (requests[i] is not { } request)
            {
                errors.Add(new ApiFieldError(path, "the destination is missing"));
                continue;
            }

            var sink = request.Sink?.Trim();
            if (string.IsNullOrEmpty(sink))
            {
                errors.Add(new ApiFieldError($"{path}.sink", "the sink is required"));
                continue;
            }

            if (!sinks.Contains(sink))
            {
                errors.Add(new ApiFieldError(
                    $"{path}.sink", $"unknown sink \"{sink}\"; available: {string.Join(", ", sinks.Order(StringComparer.Ordinal))}"));
                continue;
            }

            var parameters = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var (key, value) in request.Parameters ?? new Dictionary<string, string?>())
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    errors.Add(new ApiFieldError($"{path}.parameters", "parameter names must not be empty"));
                }
                else if (!string.IsNullOrWhiteSpace(value))
                {
                    parameters[key.Trim()] = value.Trim();
                }
            }

            if (sink == DiscordSink.SinkName)
            {
                ValidateDiscord(parameters, path, errors);
            }
            else if (sink == NtfySink.SinkName && !NtfySettings.IsValidTopic(parameters.GetValueOrDefault(NtfySink.TopicParameter)))
            {
                errors.Add(new ApiFieldError(
                    $"{path}.parameters.{NtfySink.TopicParameter}", "the topic needs 1 to 64 letters, digits, '-' or '_'"));
            }

            destinations.Add(new Destination(sink, parameters));
        }

        return destinations;
    }

    private static void ValidateDiscord(Dictionary<string, string> parameters, string path, List<ApiFieldError> errors)
    {
        if (!parameters.TryGetValue(DiscordSink.ChannelIdParameter, out var channelId))
        {
            errors.Add(new ApiFieldError($"{path}.parameters.{DiscordSink.ChannelIdParameter}", "pick a channel"));
        }
        else if (!SnowflakeComparer.IsSnowflake(channelId))
        {
            errors.Add(new ApiFieldError($"{path}.parameters.{DiscordSink.ChannelIdParameter}", "must be a numeric Discord id"));
        }

        if (parameters.TryGetValue(DiscordSink.GuildIdParameter, out var guildId) && !SnowflakeComparer.IsSnowflake(guildId))
        {
            errors.Add(new ApiFieldError($"{path}.parameters.{DiscordSink.GuildIdParameter}", "must be a numeric Discord id"));
        }
    }

    private static string? RawJson(JsonElement? element) =>
        element is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined } value ? value.GetRawText() : null;
}
