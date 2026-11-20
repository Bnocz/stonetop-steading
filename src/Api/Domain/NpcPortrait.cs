namespace Api.Domain;

// An NPC's portrait. Kept apart from Npc so listing NPCs never loads image bytes.
// The browser shrinks pictures before uploading, so 1 MB is plenty.
public sealed class NpcPortrait
{
    public const int MaxBytes = 1024 * 1024;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid NpcId { get; private set; }

    public string ContentType { get; private set; } = "";

    public byte[] Data { get; private set; } = [];

    public string Version { get; private set; } = "";

    public DateTimeOffset UploadedAt { get; private set; }

    private NpcPortrait()
    {
    }

    public static NpcPortrait Create(Guid npcId, byte[] data, DateTimeOffset uploadedAt)
    {
        var portrait = new NpcPortrait { NpcId = npcId };
        portrait.Replace(data, uploadedAt);
        return portrait;
    }

    // Throws ArgumentException unless the bytes are a PNG, JPEG or WebP of at most 1 MB.
    public void Replace(byte[] data, DateTimeOffset uploadedAt)
    {
        ContentType = ImageFormat.Validate(data, MaxBytes, "portrait");
        Data = data;
        Version = ImageFormat.Version(data);
        UploadedAt = uploadedAt;
    }
}
