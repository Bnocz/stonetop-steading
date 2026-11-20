using System.Security.Cryptography;

namespace Api.Domain;

// The region map. One per steading; uploading again replaces it.
public sealed class MapImage
{
    public const int MaxBytes = 10 * 1024 * 1024;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid SteadingId { get; private set; }

    public string ContentType { get; private set; } = "";

    public byte[] Data { get; private set; } = [];

    // Changes whenever the file does; used for the ETag and to bust browser caches.
    public string Version { get; private set; } = "";

    public DateTimeOffset UploadedAt { get; private set; }

    private MapImage()
    {
    }

    public static MapImage Create(Guid steadingId, byte[] data, DateTimeOffset uploadedAt)
    {
        var image = new MapImage { SteadingId = steadingId };
        image.Replace(data, uploadedAt);
        return image;
    }

    // Throws ArgumentException unless the bytes are a PNG, JPEG or WebP within the size limit.
    public void Replace(byte[] data, DateTimeOffset uploadedAt)
    {
        if (data.Length > MaxBytes)
        {
            throw new ArgumentException($"The map can be at most {MaxBytes / (1024 * 1024)} MB.", nameof(data));
        }

        ContentType = DetectContentType(data)
            ?? throw new ArgumentException("The map must be a PNG, JPEG or WebP image.", nameof(data));
        Data = data;
        Version = Convert.ToHexStringLower(SHA256.HashData(data))[..16];
        UploadedAt = uploadedAt;
    }

    // Decided by the file's signature, never by its name or the request's Content-Type.
    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]))
        {
            return "image/png";
        }

        if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return "image/jpeg";
        }

        if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
        {
            return "image/webp";
        }

        return null;
    }
}
