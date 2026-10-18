namespace InformadoRSS.Sinks.Discord;

/// <summary>A channel permission overwrite for a role or a member.</summary>
/// <param name="Id">The role id (the guild id for <c>@everyone</c>) or the user id.</param>
/// <param name="Type"><see cref="RoleType"/> or <see cref="MemberType"/>.</param>
/// <param name="Allow">Allowed permission bits as a decimal string.</param>
/// <param name="Deny">Denied permission bits as a decimal string.</param>
public sealed record DiscordPermissionOverwrite(string Id, int Type, string Allow, string Deny)
{
    public const int RoleType = 0;
    public const int MemberType = 1;
}
