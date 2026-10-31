using System.Collections.Immutable;
using System.Text.Json;

namespace ResinArt.Api.Infrastructure.Storage;

/// <summary>
/// File-backed JSON collection. The whole collection is held in memory (reads are lock-free) and every write
/// is persisted with an atomic "write temp file, then move over the original" so a crash can never leave a
/// half-written file. Intended for a single API instance (see architecture §5.4 for the scale-out path).
/// </summary>
public sealed class JsonFileStore<T> : IDocumentStore<T>, IDisposable where T : class, IDocument
{
    public const int CurrentSchemaVersion = 1;

    private readonly string _filePath;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private volatile ImmutableDictionary<string, T> _items;
    private long _lastWriteTicks;

    public JsonFileStore(string filePath, ILogger<JsonFileStore<T>> logger)
    {
        _filePath = Path.GetFullPath(filePath);
        _logger = logger;
        _items = Load(out _lastWriteTicks);
    }

    public string FilePath => _filePath;

    private ImmutableDictionary<string, T> GetCurrentItems()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var ticks = File.GetLastWriteTimeUtc(_filePath).Ticks;
                if (ticks != Interlocked.Read(ref _lastWriteTicks))
                {
                    _writeLock.Wait();
                    try
                    {
                        var currentTicks = File.GetLastWriteTimeUtc(_filePath).Ticks;
                        if (currentTicks != _lastWriteTicks)
                        {
                            _items = Load(out _lastWriteTicks);
                        }
                    }
                    finally
                    {
                        _writeLock.Release();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not hot-reload {Path}; using cached items.", _filePath);
        }
        return _items;
    }

    public Task<IReadOnlyList<T>> QueryAsync(Func<T, bool>? filter = null, CancellationToken ct = default)
    {
        IEnumerable<T> query = GetCurrentItems().Values;
        if (filter is not null) query = query.Where(filter);
        IReadOnlyList<T> result = query.Select(Clone).ToList();
        return Task.FromResult(result);
    }

    public Task<int> CountAsync(Func<T, bool>? filter = null, CancellationToken ct = default)
    {
        var snapshot = GetCurrentItems();
        var count = filter is null ? snapshot.Count : snapshot.Values.Count(filter);
        return Task.FromResult(count);
    }

    public Task<T?> GetAsync(string id, CancellationToken ct = default)
        => Task.FromResult(GetCurrentItems().TryGetValue(id, out var doc) ? Clone(doc) : null);

    public async Task<T> UpsertAsync(T document, int? expectedVersion = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(document.Id))
            throw new ArgumentException("Document must have an Id.", nameof(document));

        await _writeLock.WaitAsync(ct);
        try
        {
            var current = _items;
            var copy = Clone(document);

            if (current.TryGetValue(copy.Id, out var existing))
            {
                if (expectedVersion.HasValue && expectedVersion.Value != existing.Version)
                    throw new ConcurrencyException(copy.Id, expectedVersion.Value, existing.Version);
                copy.Version = existing.Version + 1;
            }
            else
            {
                if (expectedVersion is > 0)
                    throw new ConcurrencyException(copy.Id, expectedVersion.Value, 0);
                copy.Version = 1;
            }

            var updated = current.SetItem(copy.Id, copy);
            await PersistAsync(updated, ct);
            _items = updated;
            return Clone(copy);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await _writeLock.WaitAsync(ct);
        try
        {
            var current = _items;
            if (!current.ContainsKey(id)) return false;
            var updated = current.Remove(id);
            await PersistAsync(updated, ct);
            _items = updated;
            return true;
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private ImmutableDictionary<string, T> Load(out long lastWriteTicks)
    {
        if (!File.Exists(_filePath))
        {
            lastWriteTicks = 0;
            return ImmutableDictionary<string, T>.Empty.WithComparers(StringComparer.Ordinal);
        }

        lastWriteTicks = File.GetLastWriteTimeUtc(_filePath).Ticks;
        using var stream = File.OpenRead(_filePath);
        var file = JsonSerializer.Deserialize<CollectionFile<T>>(stream, JsonDefaults.Storage)
                   ?? throw new InvalidDataException($"Collection file '{_filePath}' is empty or invalid.");

        if (file.SchemaVersion > CurrentSchemaVersion)
            throw new InvalidDataException(
                $"'{_filePath}' has schema version {file.SchemaVersion}; this build supports up to {CurrentSchemaVersion}.");

        _logger.LogInformation("Loaded {Count} {Type} documents from {Path}", file.Items.Count, typeof(T).Name, _filePath);
        return file.Items.ToImmutableDictionary(d => d.Id, StringComparer.Ordinal);
    }

    private async Task PersistAsync(ImmutableDictionary<string, T> items, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var tempPath = _filePath + ".tmp";
        var file = new CollectionFile<T>
        {
            SchemaVersion = CurrentSchemaVersion,
            Items = items.Values.OrderBy(i => i.Id, StringComparer.Ordinal).ToList(),
        };

        await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                         bufferSize: 64 * 1024, FileOptions.WriteThrough))
        {
            await JsonSerializer.SerializeAsync(stream, file, JsonDefaults.Storage, ct);
        }

        File.Move(tempPath, _filePath, overwrite: true);
        Interlocked.Exchange(ref _lastWriteTicks, File.GetLastWriteTimeUtc(_filePath).Ticks);
    }

    private static T Clone(T source)
        => JsonSerializer.Deserialize<T>(JsonSerializer.SerializeToUtf8Bytes(source, JsonDefaults.Compact), JsonDefaults.Compact)!;

    public void Dispose() => _writeLock.Dispose();
}

/// <summary>On-disk envelope: <c>{ "schemaVersion": 1, "items": [ ... ] }</c>.</summary>
public sealed class CollectionFile<T>
{
    public int SchemaVersion { get; set; } = 1;
    public List<T> Items { get; set; } = [];
}
