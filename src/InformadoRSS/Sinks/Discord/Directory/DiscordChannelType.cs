namespace InformadoRSS.Sinks.Discord;

/// <summary>The Discord channel types InformadoRSS distinguishes; other values are ignored.</summary>
public enum DiscordChannelType
{
    Text = 0,
    Category = 4,
    Announcement = 5,
    Forum = 15,
}
