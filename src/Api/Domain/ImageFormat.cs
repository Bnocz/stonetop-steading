using System.Security.Cryptography;

namespace Api.Domain;

// Rules shared by every uploaded picture (the map, NPC portraits).
public static class ImageFormat
{
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

    // Changes whenever the file does; used for the ETag and to bust browser caches.
    public static string Version(byte[] data) => Convert.ToHexStringLower(SHA256.HashData(data))[..16];

    // Throws ArgumentException unless the bytes are a PNG, JPEG or WebP within the limit.
    public static string Validate(byte[] data, int maxBytes, string what)
    {
        if (data.Length > maxBytes)
        {
            throw new ArgumentException($"The {what} can be at most {FormatSize(maxBytes)}.", nameof(data));
        }

        return DetectContentType(data)
            ?? throw new ArgumentException($"The {what} must be a PNG, JPEG or WebP image.", nameof(data));
    }

    public static string FormatSize(int bytes) => $"{bytes / (1024 * 1024)} MB";
}
