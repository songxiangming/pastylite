using Microsoft.Data.Sqlite;

namespace Pasty.Data;

public class DatabaseInitializer
{
    private readonly string _connectionString;

    public DatabaseInitializer(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    public async Task InitializeAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        // Enable WAL mode for better concurrency
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode=WAL;";
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS clipboard_items (
                    id              INTEGER PRIMARY KEY AUTOINCREMENT,
                    format          INTEGER NOT NULL,
                    plain_text      TEXT,
                    rich_text       BLOB,
                    html            TEXT,
                    image_png       BLOB,
                    image_thumbnail BLOB,
                    file_paths      TEXT,
                    content_hash    TEXT NOT NULL,
                    preview_text    TEXT,
                    byte_size       INTEGER NOT NULL DEFAULT 0,
                    is_favorite     INTEGER NOT NULL DEFAULT 0,
                    created_at      TEXT NOT NULL,
                    last_pasted_at  TEXT
                );

                CREATE INDEX IF NOT EXISTS idx_clipboard_items_created_at
                    ON clipboard_items(created_at DESC);

                CREATE INDEX IF NOT EXISTS idx_clipboard_items_content_hash
                    ON clipboard_items(content_hash);
                """;
            await cmd.ExecuteNonQueryAsync();
        }

        // Migration: add ocr_text column for image OCR
        await using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "ALTER TABLE clipboard_items ADD COLUMN ocr_text TEXT;";
            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
            {
                // Column already exists — migration already applied
            }
        }
    }
}
