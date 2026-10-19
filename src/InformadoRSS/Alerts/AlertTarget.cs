namespace InformadoRSS.Alerts;

/// <summary>Where alerts are delivered; see <see cref="AlertSettings.TargetKey"/>.</summary>
public enum AlertTarget
{
    /// <summary>A Discord channel, or a direct message to a Discord user.</summary>
    Discord,

    /// <summary>A topic on the configured ntfy server.</summary>
    Ntfy,
}
