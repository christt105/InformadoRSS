using InformadoRSS.Import;

namespace InformadoRSS.Tests.Import;

/// <summary>A synthetic <c>mongoexport --jsonArray</c> dump shaped like the real MonitoRSS inventory: 19 feeds, 22 connections.</summary>
public static class MonitorssFixture
{
    public const string PanasGuild = "100000000000000001";
    public const string ZanahoriosGuild = "100000000000000002";

    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "Import", "Fixtures", "monitorss-userfeeds.json");

    public static byte[] Bytes => File.ReadAllBytes(Path);

    public static IReadOnlyList<MonitorssFeed> Read() => MonitorssExportReader.Read(Bytes);
}
