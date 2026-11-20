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
        ContentType = ImageFormat.Validate(data, MaxBytes, "map");
        Data = data;
        Version = ImageFormat.Version(data);
        UploadedAt = uploadedAt;
    }
}
