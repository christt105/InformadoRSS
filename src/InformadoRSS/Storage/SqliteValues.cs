using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

internal static class SqliteValues
{
    public static string? GetString(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    public static DateTimeOffset? GetTimestamp(SqliteDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(ordinal));

    public static object OrDbNull(string? value) => (object?)value ?? DBNull.Value;
}
