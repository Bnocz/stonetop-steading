using Api.Domain;
using Microsoft.Net.Http.Headers;

namespace Api.Images;

// HTTP plumbing shared by every uploaded picture (the map, NPC portraits).
public static class ImageHttp
{
    // Reads the raw request body, stopping one byte past the limit so oversize uploads
    // are detected without reading them whole. Returns null if the body is too large.
    public static async Task<byte[]?> ReadBodyAsync(HttpRequest request, int maxBytes, CancellationToken cancellationToken)
    {
        if (request.ContentLength > maxBytes)
        {
            return null;
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        var limit = maxBytes + 1;
        int read;
        while (buffer.Length < limit
            && (read = await request.Body.ReadAsync(
                chunk.AsMemory(0, (int)Math.Min(chunk.Length, limit - buffer.Length)),
                cancellationToken)) > 0)
        {
            buffer.Write(chunk, 0, read);
        }

        return buffer.Length > maxBytes ? null : buffer.ToArray();
    }

    // Browsers revalidate with the ETag instead of keeping a stale picture.
    public static IResult Serve(HttpContext context, byte[] data, string contentType, string version, DateTimeOffset uploadedAt)
    {
        context.Response.Headers.CacheControl = "private, no-cache";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        return Results.Bytes(
            data,
            contentType,
            lastModified: uploadedAt,
            entityTag: new EntityTagHeaderValue($"\"{version}\""));
    }

    public static IResult TooLarge(int maxBytes, string what) =>
        Results.Problem(
            detail: $"The {what} can be at most {ImageFormat.FormatSize(maxBytes)}.",
            statusCode: StatusCodes.Status413PayloadTooLarge);
}
