namespace ResinArt.Api.Infrastructure.Storage;

public static class StorageServiceCollectionExtensions
{
    /// <summary>Registers a singleton <see cref="IDocumentStore{T}"/> backed by <c>{dataRoot}/db/{fileName}</c>.</summary>
    public static IServiceCollection AddJsonCollection<T>(this IServiceCollection services, string fileName)
        where T : class, IDocument
    {
        services.AddSingleton<IDocumentStore<T>>(sp =>
        {
            var root = sp.GetRequiredService<DataRoot>();
            return new JsonFileStore<T>(Path.Combine(root.DbPath, fileName),
                sp.GetRequiredService<ILogger<JsonFileStore<T>>>());
        });
        return services;
    }
}
