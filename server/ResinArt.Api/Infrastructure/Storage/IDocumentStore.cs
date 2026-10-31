namespace ResinArt.Api.Infrastructure.Storage;

/// <summary>A JSON document persisted in a collection. <see cref="Version"/> supports optimistic concurrency.</summary>
public interface IDocument
{
    string Id { get; }
    int Version { get; set; }
}

/// <summary>
/// Storage abstraction for JSON document collections. The default implementation is
/// <see cref="JsonFileStore{T}"/>; LiteDB / MongoDB / Cosmos implementations can be swapped in later.
/// All returned documents are copies – mutating them does not affect the store until <see cref="UpsertAsync"/>.
/// </summary>
public interface IDocumentStore<T> where T : class, IDocument
{
    Task<IReadOnlyList<T>> QueryAsync(Func<T, bool>? filter = null, CancellationToken ct = default);

    Task<int> CountAsync(Func<T, bool>? filter = null, CancellationToken ct = default);

    Task<T?> GetAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Inserts or replaces a document. When <paramref name="expectedVersion"/> is supplied and does not match the
    /// stored version a <see cref="ConcurrencyException"/> is thrown. The stored version is incremented on success.
    /// </summary>
    Task<T> UpsertAsync(T document, int? expectedVersion = null, CancellationToken ct = default);

    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}

public sealed class ConcurrencyException(string id, int expected, int actual)
    : Exception($"Document '{id}' was modified by someone else (expected version {expected}, current {actual}).")
{
    public string DocumentId { get; } = id;
    public int ExpectedVersion { get; } = expected;
    public int ActualVersion { get; } = actual;
}
