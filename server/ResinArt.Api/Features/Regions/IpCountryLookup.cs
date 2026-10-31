using System.Net;
using MaxMind.GeoIP2;
using Microsoft.Extensions.Options;
using ResinArt.Api.Infrastructure.Storage;

namespace ResinArt.Api.Features.Regions;

public interface IIpCountryLookup
{
    /// <summary>Best-effort ISO country code for the caller, or null when unknown.</summary>
    string? GetCountry(HttpContext context);
}

/// <summary>
/// Resolution order: dev override → Cloudflare <c>CF-IPCountry</c> header → local GeoLite2 database.
/// The result only drives a price-display suggestion (the visitor can change it), so a spoofed header is low risk;
/// still, disable <see cref="GeoIpOptions.UseCloudflareHeader"/> when not deployed behind Cloudflare.
/// </summary>
public sealed class IpCountryLookup(IOptionsMonitor<GeoIpOptions> options, DataRoot dataRoot, ILogger<IpCountryLookup> logger)
    : IIpCountryLookup, IDisposable
{
    private readonly Lock _gate = new();
    private DatabaseReader? _reader;
    private string? _readerPath;

    public string? GetCountry(HttpContext context)
    {
        var opts = options.CurrentValue;

        if (!string.IsNullOrWhiteSpace(opts.DevCountryOverride))
            return opts.DevCountryOverride.Trim().ToUpperInvariant();

        if (opts.UseCloudflareHeader
            && context.Request.Headers.TryGetValue("CF-IPCountry", out var header)
            && header.ToString() is { Length: 2 } cc
            && cc is not ("XX" or "T1"))
        {
            return cc.ToUpperInvariant();
        }

        var ip = context.Connection.RemoteIpAddress;
        if (ip is null || IPAddress.IsLoopback(ip)) return null;

        var reader = GetReader(opts);
        if (reader is null) return null;
        try
        {
            return reader.TryCountry(ip, out var response) ? response?.Country.IsoCode : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "GeoIP lookup failed");
            return null;
        }
    }

    private DatabaseReader? GetReader(GeoIpOptions opts)
    {
        if (string.IsNullOrWhiteSpace(opts.FallbackDatabase)) return null;
        var path = dataRoot.Resolve(opts.FallbackDatabase);

        lock (_gate)
        {
            if (_reader is not null && _readerPath == path) return _reader;
            if (!File.Exists(path)) return null;
            _reader?.Dispose();
            _reader = new DatabaseReader(path);
            _readerPath = path;
            logger.LogInformation("Loaded GeoIP database {Path}", path);
            return _reader;
        }
    }

    public void Dispose() => _reader?.Dispose();
}
