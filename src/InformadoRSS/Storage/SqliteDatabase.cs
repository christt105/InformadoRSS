using Microsoft.Data.Sqlite;

namespace InformadoRSS.Storage;

/// <summary>Opens connections to the application database with foreign keys enforced.</summary>
public sealed class SqliteDatabase
{
    /// <summary>Configuration key holding the database file path.</summary>
    public const string PathConfigKey = "Database:Path";

    public const string DefaultPath = "/data/informadorss.db";

    private readonly string _connectionString;

    public SqliteDatabase(string connectionString)
    {
        _connectionString = new SqliteConnectionStringBuilder(connectionString) { ForeignKeys = true }.ToString();
    }

    /// <summary>Creates a database backed by the file at <paramref name="path"/>, creating its directory if needed.</summary>
    public static SqliteDatabase FromPath(string path)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        return new SqliteDatabase(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString());
    }

    public async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
