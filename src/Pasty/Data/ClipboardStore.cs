using Microsoft.Data.Sqlite;
using Pasty.Models;

namespace Pasty.Data;

public class ClipboardStore : IDisposable
{
    private readonly string _connectionString;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public ClipboardStore(string dbPath)
    {
        _connectionString = $"Data Source={dbPath}";
    }

    private async Task<SqliteConnection> OpenConnectionAsync()
    {
        var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Insert a new item, or bump an existing duplicate to the top.
    /// Returns the item's ID.
    /// </summary>
    public async Task<long> InsertOrBumpAsync(ClipboardItem item)
    {
        await _lock.WaitAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();

            // Check for duplicate via content_hash
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT id FROM clipboard_items WHERE content_hash = @hash LIMIT 1;";
                cmd.Parameters.AddWithValue("@hash", item.ContentHash);
                var result = await cmd.ExecuteScalarAsync();
                if (result is long existingId)
                {
                    // Bump to top by updating created_at
                    await using var updateCmd = conn.CreateCommand();
                    updateCmd.CommandText = "UPDATE clipboard_items SET created_at = @now WHERE id = @id;";
                    updateCmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
                    updateCmd.Parameters.AddWithValue("@id", existingId);
                    await updateCmd.ExecuteNonQueryAsync();
                    return existingId;
                }
            }

            // Insert new
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = """
                    INSERT INTO clipboard_items
                        (format, plain_text, rich_text, html, image_png, image_thumbnail,
                         file_paths, content_hash, preview_text, byte_size, created_at, ocr_text)
                    VALUES
                        (@format, @plain_text, @rich_text, @html, @image_png, @image_thumbnail,
                         @file_paths, @content_hash, @preview_text, @byte_size, @created_at, @ocr_text);
                    SELECT last_insert_rowid();
                    """;
                cmd.Parameters.AddWithValue("@format", (int)item.Format);
                cmd.Parameters.AddWithValue("@plain_text", (object?)item.PlainText ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@rich_text", (object?)item.RichText ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@html", (object?)item.Html ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@image_png", (object?)item.ImagePng ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@image_thumbnail", (object?)item.ImageThumbnail ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@file_paths", (object?)item.FilePaths ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@content_hash", item.ContentHash);
                cmd.Parameters.AddWithValue("@preview_text", (object?)item.PreviewText ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@byte_size", item.ByteSize);
                cmd.Parameters.AddWithValue("@created_at", item.CreatedAt.ToString("o"));
                cmd.Parameters.AddWithValue("@ocr_text", (object?)item.OcrText ?? DBNull.Value);

                var newId = (long)(await cmd.ExecuteScalarAsync())!;
                item.Id = newId;
                return newId;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get the most recent items (lightweight: no full image blobs).
    /// </summary>
    public async Task<List<ClipboardItem>> GetRecentAsync(int limit = 200)
    {
        await _lock.WaitAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, format, preview_text, image_thumbnail, file_paths,
                       content_hash, byte_size, is_favorite, created_at, last_pasted_at,
                       image_png, ocr_text
                FROM clipboard_items
                ORDER BY created_at DESC
                LIMIT @limit;
                """;
            cmd.Parameters.AddWithValue("@limit", limit);

            var items = new List<ClipboardItem>();
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(ReadListItem(reader));
            }
            return items;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Get a single item with full content (including image_png, rich_text, html).
    /// </summary>
    public async Task<ClipboardItem?> GetByIdAsync(long id)
    {
        await _lock.WaitAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT id, format, plain_text, rich_text, html, image_png, image_thumbnail,
                       file_paths, content_hash, preview_text, byte_size, is_favorite,
                       created_at, last_pasted_at, ocr_text
                FROM clipboard_items
                WHERE id = @id;
                """;
            cmd.Parameters.AddWithValue("@id", id);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync()) return null;

            return new ClipboardItem
            {
                Id = reader.GetInt64(0),
                Format = (ClipboardFormat)reader.GetInt32(1),
                PlainText = reader.IsDBNull(2) ? null : reader.GetString(2),
                RichText = reader.IsDBNull(3) ? null : (byte[])reader[3],
                Html = reader.IsDBNull(4) ? null : reader.GetString(4),
                ImagePng = reader.IsDBNull(5) ? null : (byte[])reader[5],
                ImageThumbnail = reader.IsDBNull(6) ? null : (byte[])reader[6],
                FilePaths = reader.IsDBNull(7) ? null : reader.GetString(7),
                ContentHash = reader.GetString(8),
                PreviewText = reader.IsDBNull(9) ? null : reader.GetString(9),
                ByteSize = reader.GetInt64(10),
                IsFavorite = reader.GetInt32(11) != 0,
                CreatedAt = DateTime.Parse(reader.GetString(12)),
                LastPastedAt = reader.IsDBNull(13) ? null : DateTime.Parse(reader.GetString(13)),
                OcrText = reader.IsDBNull(14) ? null : reader.GetString(14)
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateLastPastedAsync(long id)
    {
        await _lock.WaitAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE clipboard_items SET last_pasted_at = @now WHERE id = @id;";
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("o"));
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task UpdateOcrTextAsync(long id, string ocrText)
    {
        await _lock.WaitAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE clipboard_items SET ocr_text = @ocr_text WHERE id = @id;";
            cmd.Parameters.AddWithValue("@ocr_text", ocrText);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task DeleteAsync(long id)
    {
        await _lock.WaitAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM clipboard_items WHERE id = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task PruneAsync(int keepCount = 1000)
    {
        await _lock.WaitAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                DELETE FROM clipboard_items
                WHERE id NOT IN (
                    SELECT id FROM clipboard_items
                    ORDER BY created_at DESC
                    LIMIT @keep
                );
                """;
            cmd.Parameters.AddWithValue("@keep", keepCount);
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ClearAllAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await using var conn = await OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM clipboard_items;";
            await cmd.ExecuteNonQueryAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    private static ClipboardItem ReadListItem(SqliteDataReader reader)
    {
        return new ClipboardItem
        {
            Id = reader.GetInt64(0),
            Format = (ClipboardFormat)reader.GetInt32(1),
            PreviewText = reader.IsDBNull(2) ? null : reader.GetString(2),
            ImageThumbnail = reader.IsDBNull(3) ? null : (byte[])reader[3],
            FilePaths = reader.IsDBNull(4) ? null : reader.GetString(4),
            ContentHash = reader.GetString(5),
            ByteSize = reader.GetInt64(6),
            IsFavorite = reader.GetInt32(7) != 0,
            CreatedAt = DateTime.Parse(reader.GetString(8)),
            LastPastedAt = reader.IsDBNull(9) ? null : DateTime.Parse(reader.GetString(9)),
            ImagePng = reader.IsDBNull(10) ? null : (byte[])reader[10],
            OcrText = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
