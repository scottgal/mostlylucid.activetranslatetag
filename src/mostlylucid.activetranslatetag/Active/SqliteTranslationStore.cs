using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace mostlylucid.activetranslatetag.Active;

/// <summary>Direct primary-key lookups, prepared SQL, WAL and pooled connections; no ORM on this path.</summary>
public sealed class SqliteTranslationStore : IDisposable
{
    private readonly string _connectionString;
    private readonly MemoryCache _hot;
    private readonly SemaphoreSlim _initialize = new(1);
    private bool _initialized;

    public SqliteTranslationStore(IOptions<ActiveTranslationOptions> options)
    {
        var path = Path.GetFullPath(options.Value.DatabasePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Pooling = true, DefaultTimeout = 5 }.ToString();
        _hot = new MemoryCache(new MemoryCacheOptions { SizeLimit = options.Value.MemoryCacheEntries });
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqliteConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
            if (!_initialized)
            {
                await _initialize.WaitAsync(ct);
                try
                {
                    if (!_initialized)
                    {
                        using var command = connection.CreateCommand();
                        command.CommandText = """
                            PRAGMA journal_mode=WAL;
                            CREATE TABLE IF NOT EXISTS active_translations (
                                id TEXT PRIMARY KEY, request TEXT NOT NULL, html TEXT,
                                created_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                            ) WITHOUT ROWID;
                            """;
                        await command.ExecuteNonQueryAsync(ct);
                        _initialized = true;
                    }
                }
                finally { _initialize.Release(); }
            }
            return connection;
        }
        catch { await connection.DisposeAsync(); throw; }
    }

    public async Task SaveRequestAsync(TranslationRequest request, CancellationToken ct = default)
    {
        using var connection = await OpenAsync(ct);
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO active_translations (id, request) VALUES ($id, $request)";
        command.Parameters.AddWithValue("$id", request.Id);
        command.Parameters.AddWithValue("$request", JsonSerializer.Serialize(request));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<TranslationRequest?> GetRequestAsync(string id, CancellationToken ct = default)
    {
        using var connection = await OpenAsync(ct);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT request FROM active_translations WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        var value = await command.ExecuteScalarAsync(ct);
        return value is string json ? JsonSerializer.Deserialize<TranslationRequest>(json) : null;
    }

    public async Task<string?> GetHtmlAsync(string id, CancellationToken ct = default)
    {
        if (_hot.TryGetValue<string>(id, out var cached)) return cached;
        using var connection = await OpenAsync(ct);
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT html FROM active_translations WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        var value = await command.ExecuteScalarAsync(ct);
        if (value is not string html) return null;
        Remember(id, html);
        return html;
    }

    public async Task SetHtmlAsync(string id, string html, CancellationToken ct = default)
    {
        using var connection = await OpenAsync(ct);
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE active_translations SET html = $html WHERE id = $id";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$html", html);
        await command.ExecuteNonQueryAsync(ct);
        Remember(id, html);
    }

    private void Remember(string id, string html) => _hot.Set(id, html,
        new MemoryCacheEntryOptions().SetSize(1).SetSlidingExpiration(TimeSpan.FromMinutes(20)));

    public void Dispose() { _hot.Dispose(); _initialize.Dispose(); }
}
