namespace InformadoRSS.Sinks.Discord;

/// <summary>
/// Computes a member's effective permissions in a guild channel the way Discord does: the guild owner and
/// <see cref="DiscordPermissions.Administrator"/> get everything; otherwise <c>@everyone</c> plus the member's roles,
/// then the channel overwrites for <c>@everyone</c>, the member's roles (combined) and the member, in that order.
/// </summary>
public static class DiscordPermissionCalculator
{
    public static ulong ComputeBase(DiscordGuild guild, DiscordGuildMember member, string userId)
    {
        ArgumentNullException.ThrowIfNull(guild);
        ArgumentNullException.ThrowIfNull(member);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        if (string.Equals(guild.OwnerId, userId, StringComparison.Ordinal))
        {
            return DiscordPermissions.All;
        }

        var roles = guild.Roles.ToDictionary(role => role.Id, StringComparer.Ordinal);
        var permissions = roles.TryGetValue(guild.Id, out var everyone)
            ? DiscordPermissions.Parse(everyone.Permissions)
            : DiscordPermissions.None;

        foreach (var roleId in member.Roles)
        {
            if (roles.TryGetValue(roleId, out var role))
            {
                permissions |= DiscordPermissions.Parse(role.Permissions);
            }
        }

        return (permissions & DiscordPermissions.Administrator) != 0 ? DiscordPermissions.All : permissions;
    }

    /// <summary>
    /// Applies the channel overwrites to the base permissions, then drops what Discord implicitly denies: everything
    /// without <see cref="DiscordPermissions.ViewChannel"/>, and <see cref="DiscordPermissions.DependOnSendMessages"/>
    /// without <see cref="DiscordPermissions.SendMessages"/>.
    /// </summary>
    public static ulong Compute(DiscordGuild guild, DiscordGuildMember member, string userId, DiscordChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var permissions = ComputeBase(guild, member, userId);
        if (permissions == DiscordPermissions.All)
        {
            return DiscordPermissions.All;
        }

        var overwrites = channel.PermissionOverwrites ?? [];

        var everyone = overwrites.FirstOrDefault(overwrite =>
            overwrite.Type == DiscordPermissionOverwrite.RoleType && string.Equals(overwrite.Id, guild.Id, StringComparison.Ordinal));
        if (everyone is not null)
        {
            permissions = Apply(permissions, DiscordPermissions.Parse(everyone.Allow), DiscordPermissions.Parse(everyone.Deny));
        }

        var memberRoles = member.Roles.ToHashSet(StringComparer.Ordinal);
        var roleAllow = DiscordPermissions.None;
        var roleDeny = DiscordPermissions.None;
        foreach (var overwrite in overwrites)
        {
            if (overwrite.Type == DiscordPermissionOverwrite.RoleType
                && !string.Equals(overwrite.Id, guild.Id, StringComparison.Ordinal)
                && memberRoles.Contains(overwrite.Id))
            {
                roleAllow |= DiscordPermissions.Parse(overwrite.Allow);
                roleDeny |= DiscordPermissions.Parse(overwrite.Deny);
            }
        }

        permissions = Apply(permissions, roleAllow, roleDeny);

        var own = overwrites.FirstOrDefault(overwrite =>
            overwrite.Type == DiscordPermissionOverwrite.MemberType && string.Equals(overwrite.Id, userId, StringComparison.Ordinal));
        if (own is not null)
        {
            permissions = Apply(permissions, DiscordPermissions.Parse(own.Allow), DiscordPermissions.Parse(own.Deny));
        }

        if ((permissions & DiscordPermissions.ViewChannel) == 0)
        {
            return DiscordPermissions.None;
        }

        if ((permissions & DiscordPermissions.SendMessages) == 0)
        {
            permissions &= ~DiscordPermissions.DependOnSendMessages;
        }

        return permissions;
    }

    /// <summary>
    /// Whether the member is timed out at <paramref name="now"/>. Discord leaves a timed-out member only
    /// <see cref="DiscordPermissions.ViewChannel"/> and <see cref="DiscordPermissions.ReadMessageHistory"/>, unless they
    /// own the guild or are an administrator.
    /// </summary>
    public static bool IsTimedOut(DiscordGuildMember member, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(member);
        return member.CommunicationDisabledUntil is { } until && until > now;
    }

    /// <summary>Drops what a timeout takes away from <paramref name="permissions"/> computed by <see cref="Compute"/>.</summary>
    public static ulong ApplyTimeout(ulong permissions) =>
        permissions == DiscordPermissions.All
            ? permissions
            : permissions & (DiscordPermissions.ViewChannel | DiscordPermissions.ReadMessageHistory);

    private static ulong Apply(ulong permissions, ulong allow, ulong deny) => (permissions & ~deny) | allow;
}
