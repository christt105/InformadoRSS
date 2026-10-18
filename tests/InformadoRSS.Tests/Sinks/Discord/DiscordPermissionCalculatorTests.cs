using InformadoRSS.Sinks.Discord;

namespace InformadoRSS.Tests.Sinks.Discord;

public sealed class DiscordPermissionCalculatorTests
{
    private const string GuildId = "100";
    private const string UserId = "900";
    private const string RoleA = "201";
    private const string RoleB = "202";
    private const string OtherRole = "203";

    private const ulong View = DiscordPermissions.ViewChannel;
    private const ulong Send = DiscordPermissions.SendMessages;
    private const ulong Embed = DiscordPermissions.EmbedLinks;

    private static DiscordGuild Guild(ulong everyone, ulong roleA = 0, ulong roleB = 0, string ownerId = "1") =>
        new(
            GuildId,
            "guild",
            ownerId,
            [
                new DiscordRole(GuildId, "@everyone", everyone.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new DiscordRole(RoleA, "a", roleA.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new DiscordRole(RoleB, "b", roleB.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new DiscordRole(OtherRole, "other", "0"),
            ]);

    private static DiscordGuildMember Member(params string[] roles) => new(roles);

    private static DiscordPermissionOverwrite Overwrite(string id, int type, ulong allow = 0, ulong deny = 0) =>
        new(
            id,
            type,
            allow.ToString(System.Globalization.CultureInfo.InvariantCulture),
            deny.ToString(System.Globalization.CultureInfo.InvariantCulture));

    private static DiscordChannel Channel(params DiscordPermissionOverwrite[] overwrites) =>
        new("300", (int)DiscordChannelType.Text, "channel", PermissionOverwrites: overwrites);

    [Fact]
    public void BaseCombinesEveryoneAndMemberRoles()
    {
        var permissions = DiscordPermissionCalculator.ComputeBase(Guild(View, roleA: Send, roleB: Embed), Member(RoleA), UserId);

        Assert.Equal(View | Send, permissions);
    }

    [Fact]
    public void UnknownRolesAndBadPermissionStringsCountAsNothing()
    {
        var guild = new DiscordGuild(GuildId, "guild", "1", [new DiscordRole(GuildId, "@everyone", "not a number")]);

        Assert.Equal(DiscordPermissions.None, DiscordPermissionCalculator.ComputeBase(guild, Member("999"), UserId));
    }

    [Fact]
    public void OwnerGetsEverythingDespiteOverwrites()
    {
        var channel = Channel(Overwrite(UserId, DiscordPermissionOverwrite.MemberType, deny: View | Send));

        var permissions = DiscordPermissionCalculator.Compute(Guild(0, ownerId: UserId), Member(), UserId, channel);

        Assert.Equal(DiscordPermissions.All, permissions);
    }

    [Fact]
    public void AdministratorBypassesOverwrites()
    {
        var channel = Channel(Overwrite(GuildId, DiscordPermissionOverwrite.RoleType, deny: View));

        var permissions = DiscordPermissionCalculator.Compute(Guild(0, roleA: DiscordPermissions.Administrator), Member(RoleA), UserId, channel);

        Assert.Equal(DiscordPermissions.All, permissions);
    }

    [Fact]
    public void NoOverwritesKeepsBasePermissions()
    {
        Assert.Equal(View | Send | Embed, DiscordPermissionCalculator.Compute(Guild(View | Send | Embed), Member(), UserId, Channel()));
    }

    [Fact]
    public void EveryoneOverwriteDenyingViewHidesTheChannel()
    {
        var channel = Channel(Overwrite(GuildId, DiscordPermissionOverwrite.RoleType, deny: View));

        Assert.Equal(DiscordPermissions.None, DiscordPermissionCalculator.Compute(Guild(View | Send | Embed), Member(), UserId, channel));
    }

    [Fact]
    public void RoleOverwriteAllowWinsOverEveryoneDeny()
    {
        var channel = Channel(
            Overwrite(GuildId, DiscordPermissionOverwrite.RoleType, deny: View | Send),
            Overwrite(RoleA, DiscordPermissionOverwrite.RoleType, allow: View | Send));

        Assert.Equal(View | Send | Embed, DiscordPermissionCalculator.Compute(Guild(View | Send | Embed), Member(RoleA), UserId, channel));
    }

    [Fact]
    public void RoleOverwritesAreCombinedWithAllowWinning()
    {
        var channel = Channel(
            Overwrite(RoleA, DiscordPermissionOverwrite.RoleType, deny: Send),
            Overwrite(RoleB, DiscordPermissionOverwrite.RoleType, allow: Send));

        Assert.Equal(View | Send, DiscordPermissionCalculator.Compute(Guild(View | Send), Member(RoleA, RoleB), UserId, channel));
    }

    [Fact]
    public void OverwritesForRolesTheMemberLacksAreIgnored()
    {
        var channel = Channel(Overwrite(OtherRole, DiscordPermissionOverwrite.RoleType, deny: Send));

        Assert.Equal(View | Send, DiscordPermissionCalculator.Compute(Guild(View | Send), Member(RoleA), UserId, channel));
    }

    [Fact]
    public void MemberOverwriteIsAppliedLast()
    {
        var channel = Channel(
            Overwrite(RoleA, DiscordPermissionOverwrite.RoleType, allow: Send),
            Overwrite(UserId, DiscordPermissionOverwrite.MemberType, deny: Send),
            Overwrite("777", DiscordPermissionOverwrite.MemberType, allow: Embed));

        Assert.Equal(View, DiscordPermissionCalculator.Compute(Guild(View), Member(RoleA), UserId, channel));
    }

    [Fact]
    public void MemberOverwriteIdMatchingARoleIdIsNotTreatedAsRole()
    {
        var channel = Channel(Overwrite(RoleA, DiscordPermissionOverwrite.MemberType, deny: Send));

        Assert.Equal(View | Send, DiscordPermissionCalculator.Compute(Guild(View | Send), Member(RoleA), UserId, channel));
    }

    [Fact]
    public void WithoutSendMessagesEmbedLinksIsImplicitlyLost()
    {
        var channel = Channel(Overwrite(GuildId, DiscordPermissionOverwrite.RoleType, deny: Send));

        Assert.Equal(View, DiscordPermissionCalculator.Compute(Guild(View | Send | Embed), Member(), UserId, channel));
    }
}
