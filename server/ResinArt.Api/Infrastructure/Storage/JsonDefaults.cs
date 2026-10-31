using System.Text.Json;
using System.Text.Json.Serialization;

namespace ResinArt.Api.Infrastructure.Storage;

public static class JsonDefaults
{
    /// <summary>Options shared by HTTP responses and on-disk JSON so documents look identical everywhere.</summary>
    public static void Apply(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DictionaryKeyPolicy = null; // keep region codes such as "GJ-IN" as-is
        options.PropertyNameCaseInsensitive = true;
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    public static readonly JsonSerializerOptions Storage = Create(indented: true);

    /// <summary>Non-indented options for fast in-memory deep cloning without whitespace formatting overhead.</summary>
    public static readonly JsonSerializerOptions Compact = Create(indented: false);

    private static JsonSerializerOptions Create(bool indented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = indented };
        Apply(options);
        return options;
    }
}
