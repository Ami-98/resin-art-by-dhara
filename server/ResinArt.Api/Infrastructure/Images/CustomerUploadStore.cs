using Microsoft.Extensions.Options;
using ResinArt.Api.Infrastructure.Settings;

namespace ResinArt.Api.Infrastructure.Images;

/// <summary>
/// Validates uploaded customer and product images (magic-byte signature check + size limit), strips privacy-sensitive
/// metadata segments (JPEG APP1/EXIF/XMP/GPS and PNG eXIf/tEXt/iTXt/zTXt chunks), stores temporary uploads in
/// <c>{dataRoot}/uploads/temp</c>, and promotes them to permanent <c>/uploads/requests/{requestId}</c> on submit.
/// </summary>
public sealed class CustomerUploadStore
{
    public const long DefaultMaxUploadBytes = 8 * 1024 * 1024; // 8 MB fallback

    private readonly string _uploadsRoot;
    private readonly IOptionsMonitor<UploadOptions>? _options;
    private readonly ILogger<CustomerUploadStore> _logger;

    public CustomerUploadStore(
        Storage.DataRoot dataRoot,
        ILogger<CustomerUploadStore> logger,
        IOptionsMonitor<UploadOptions>? options = null)
    {
        _uploadsRoot = dataRoot.UploadsPath;
        _logger = logger;
        _options = options;
        Directory.CreateDirectory(TempDir);
        Directory.CreateDirectory(RequestsDir);
    }

    private UploadOptions CurrentOptions => _options?.CurrentValue ?? new UploadOptions();

    public string TempDir => Path.Combine(_uploadsRoot, "temp");
    public string RequestsDir => Path.Combine(_uploadsRoot, "requests");

    /// <summary>
    /// Reads, validates magic-byte image format, enforces configured size limits, and strips EXIF/text metadata.
    /// Shared by both customer temp uploads and admin product gallery uploads.
    /// </summary>
    public async Task<(byte[] Bytes, string Format)> ReadAndSanitizeAsync(
        Stream stream,
        long length,
        CancellationToken ct = default)
    {
        var opts = CurrentOptions;
        var maxBytes = opts.MaxUploadBytes > 0 ? opts.MaxUploadBytes : DefaultMaxUploadBytes;

        if (length <= 0)
            throw new InvalidOperationException("Uploaded file is empty.");
        if (length > maxBytes)
            throw new InvalidOperationException($"Image exceeds the {opts.MaxFileSizeMb} MB limit.");

        using var ms = new MemoryStream((int)Math.Min(length, maxBytes));
        await stream.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();

        if (bytes.Length > maxBytes)
            throw new InvalidOperationException($"Image exceeds the {opts.MaxFileSizeMb} MB limit.");

        var format = DetectFormat(bytes)
            ?? throw new InvalidOperationException("Unsupported image format. Please upload a JPEG, PNG or WebP image.");

        if (!opts.StripExifMetadata)
            return (bytes, format);

        var cleaned = format switch
        {
            "jpg" => StripJpegExif(bytes),
            "png" => StripPngTextAndExif(bytes),
            _ => bytes
        };

        return (cleaned, format);
    }

    public async Task<TempUploadResult> SaveTempAsync(Stream stream, long length, string originalFileName, CancellationToken ct = default)
    {
        var (cleaned, format) = await ReadAndSanitizeAsync(stream, length, ct);
        var prefix = string.IsNullOrWhiteSpace(CurrentOptions.TempFilePrefix) ? "tmp_" : CurrentOptions.TempFilePrefix;

        var tempId = $"{prefix}{Guid.NewGuid():N}.{format}";
        var fullPath = Path.Combine(TempDir, tempId);
        await File.WriteAllBytesAsync(fullPath, cleaned, ct);

        _logger.LogInformation("Saved temp customer upload {TempId} ({Bytes} bytes, original {Original})",
            tempId, cleaned.Length, Path.GetFileName(originalFileName));

        return new TempUploadResult(tempId, $"/uploads/temp/{tempId}", cleaned.Length);
    }

    /// <summary>Moves validated temp uploads into <c>/uploads/requests/{requestId}/</c> and returns their public URLs.</summary>
    public IReadOnlyList<string> PromoteTempUploads(string requestId, IEnumerable<string>? tempIds)
    {
        if (tempIds is null) return [];
        var opts = CurrentOptions;
        var tempPrefix = string.IsNullOrWhiteSpace(opts.TempFilePrefix) ? "tmp_" : opts.TempFilePrefix;
        var refPrefix = string.IsNullOrWhiteSpace(opts.ReferenceFilePrefix) ? "ref_" : opts.ReferenceFilePrefix;
        var maxFiles = Math.Max(1, opts.MaxFilesPerRequest);

        var urls = new List<string>();
        var sanitizedRequestId = SanitizeSegment(requestId);
        var targetDir = Path.Combine(RequestsDir, sanitizedRequestId);

        foreach (var rawId in tempIds.Distinct(StringComparer.OrdinalIgnoreCase).Take(maxFiles))
        {
            var fileName = Path.GetFileName(rawId);
            if (!fileName.StartsWith(tempPrefix, StringComparison.Ordinal) || !IsSafeTempFileName(fileName))
                continue;

            var sourcePath = Path.Combine(TempDir, fileName);
            if (!File.Exists(sourcePath))
                continue;

            Directory.CreateDirectory(targetDir);
            var ext = Path.GetExtension(fileName);
            var finalName = $"{refPrefix}{urls.Count + 1}{ext}";
            var destPath = Path.Combine(targetDir, finalName);
            File.Move(sourcePath, destPath, overwrite: true);
            urls.Add($"/uploads/requests/{sanitizedRequestId}/{finalName}");
        }

        return urls;
    }

    public static string? DetectFormat(ReadOnlySpan<byte> bytes)
    {
        // JPEG: FF D8 FF
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            return "jpg";

        // PNG: 89 50 4E 47 0D 0A 1A 0A
        if (bytes.Length >= 8 &&
            bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
            bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A)
            return "png";

        // WebP: "RIFF" .... "WEBP"
        if (bytes.Length >= 12 &&
            bytes[0] == (byte)'R' && bytes[1] == (byte)'I' && bytes[2] == (byte)'F' && bytes[3] == (byte)'F' &&
            bytes[8] == (byte)'W' && bytes[9] == (byte)'E' && bytes[10] == (byte)'B' && bytes[11] == (byte)'P')
            return "webp";

        return null;
    }

    /// <summary>
    /// Removes JPEG APP1–APP15 (0xFFE1..0xFFEF) and COM (0xFFFE) segments which hold EXIF, GPS coordinates and XMP
    /// metadata while preserving APP0 (JFIF), quantization/Huffman tables and the compressed image stream without re-encoding.
    /// </summary>
    public static byte[] StripJpegExif(byte[] data)
    {
        if (data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8) return data;

        using var output = new MemoryStream(data.Length);
        output.WriteByte(0xFF);
        output.WriteByte(0xD8);

        int i = 2;
        while (i + 1 < data.Length)
        {
            if (data[i] != 0xFF)
            {
                // Corrupt marker structure; return original bytes rather than breaking the image
                return data;
            }

            while (i < data.Length && data[i] == 0xFF) i++;
            if (i >= data.Length) break;

            byte marker = data[i++];

            // Start of Scan (SOS, 0xDA) or End of Image (EOI, 0xD9) -> copy everything remaining verbatim
            if (marker == 0xDA || marker == 0xD9)
            {
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                if (i < data.Length)
                    output.Write(data, i, data.Length - i);
                return output.ToArray();
            }

            // Standalone markers without length
            if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
            {
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                continue;
            }

            if (i + 1 >= data.Length) return data;
            int segLen = (data[i] << 8) | data[i + 1];
            if (segLen < 2 || i + segLen > data.Length) return data;

            bool isPrivacyMetadata = (marker >= 0xE1 && marker <= 0xEF) || marker == 0xFE;
            if (!isPrivacyMetadata)
            {
                output.WriteByte(0xFF);
                output.WriteByte(marker);
                output.Write(data, i, segLen);
            }

            i += segLen;
        }

        return output.ToArray();
    }

    private static byte[] StripPngTextAndExif(byte[] data)
    {
        if (data.Length < 8) return data;
        using var output = new MemoryStream(data.Length);
        output.Write(data, 0, 8); // PNG signature

        int i = 8;
        while (i + 8 <= data.Length)
        {
            int length = (data[i] << 24) | (data[i + 1] << 16) | (data[i + 2] << 8) | data[i + 3];
            if (length < 0 || i + 12 + length > data.Length) return data;

            var type = System.Text.Encoding.ASCII.GetString(data, i + 4, 4);
            bool strip = type is "eXIf" or "tEXt" or "zTXt" or "iTXt";
            int totalChunk = 12 + length;

            if (!strip)
                output.Write(data, i, totalChunk);

            i += totalChunk;
            if (type == "IEND") break;
        }

        return output.ToArray();
    }

    private static bool IsSafeTempFileName(string name)
        => name.All(c => char.IsLetterOrDigit(c) || c is '_' or '.');

    private static string SanitizeSegment(string value)
        => new(value.Where(c => char.IsLetterOrDigit(c) || c is '_' or '-').ToArray());
}

public sealed record TempUploadResult(string TempId, string PreviewUrl, int SizeBytes);
