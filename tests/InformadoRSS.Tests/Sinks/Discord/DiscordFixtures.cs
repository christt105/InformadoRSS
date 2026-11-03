namespace InformadoRSS.Tests.Sinks.Discord;

/// <summary>A small guild as Discord would describe it, for directory and endpoint tests.</summary>
public static class DiscordFixtures
{
    public const string BotId = "900";
    public const string GuildId = "100";
    public const string BotRoleId = "201";
    public const string MutedRoleId = "202";
    public const string GeneralId = "301";
    public const string NewsId = "302";
    public const string ForumId = "303";
    public const string VoiceId = "304";
    public const string CategoryId = "310";
    public const string AlertsId = "311";
    public const string HiddenId = "312";

    public const ulong ViewSendEmbed = (1UL << 10) | (1UL << 11) | (1UL << 14);

    public static string CurrentUser => $$"""{"id":"{{BotId}}","username":"InformadoRSS","bot":true}""";

    public static string Guilds =>
        """
        [
          {"id":"100","name":"los panas","icon":"abc","owner":false,"permissions":"0"},
          {"id":"50","name":"Zanahorios","icon":null},
          {"id":"60","name":"animated","icon":"a_def"}
        ]
        """;

    public static string Guild(string ownerId = "1", ulong everyone = ViewSendEmbed, ulong botRole = 0) =>
        $$"""
        {
          "id":"{{GuildId}}","name":"los panas","owner_id":"{{ownerId}}",
          "roles":[
            {"id":"{{GuildId}}","name":"@everyone","permissions":"{{everyone}}","position":0},
            {"id":"{{BotRoleId}}","name":"Bots","permissions":"{{botRole}}","position":2},
            {"id":"{{MutedRoleId}}","name":"Muted","permissions":"0","position":1}
          ]
        }
        """;

    public static string Member(params string[] roles) => MemberTimedOutUntil(null, roles);

    public static string MemberTimedOutUntil(string? until, params string[] roles)
    {
        var roleList = string.Join(',', roles.Select(role => $"\"{role}\""));
        var timeout = until is null ? "null" : $"\"{until}\"";
        return $$"""{"roles":[{{roleList}}],"user":{"id":"{{BotId}}"},"communication_disabled_until":{{timeout}} }""";
    }

    public static string Channels =>
        $$"""
        [
          {"id":"{{CategoryId}}","type":4,"name":"Feeds","position":1},
          {"id":"{{AlertsId}}","type":0,"name":"alerts","position":5,"parent_id":"{{CategoryId}}"},
          {"id":"{{HiddenId}}","type":0,"name":"hidden","position":2,"parent_id":"{{CategoryId}}",
           "permission_overwrites":[{"id":"{{GuildId}}","type":0,"allow":"0","deny":"1024"}]},
          {"id":"{{VoiceId}}","type":2,"name":"Voice","position":0},
          {"id":"{{ForumId}}","type":15,"name":"posts","position":3},
          {"id":"{{NewsId}}","type":5,"name":"news","position":2,
           "permission_overwrites":[{"id":"{{GuildId}}","type":0,"allow":"0","deny":"16384"}]},
          {"id":"{{GeneralId}}","type":0,"name":"general","position":1}
        ]
        """;
}
