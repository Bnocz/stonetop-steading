namespace Api.Domain;

// An NPC the party has met. GmNotes and GmOnly NPCs must never reach players.
public sealed class Npc
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;
    public const int GmNotesMaxLength = 4000;

    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid SteadingId { get; private set; }

    public string Name { get; private set; } = "";

    public string Description { get; private set; } = "";

    public string GmNotes { get; private set; } = "";

    public bool GmOnly { get; private set; }

    // Deleting archives instead, so the GM can restore an NPC.
    public DateTimeOffset? ArchivedAt { get; private set; }

    public bool Archived => ArchivedAt is not null;

    private Npc()
    {
    }

    public static Npc Create(Guid steadingId, string? name, string? description, string? gmNotes, bool gmOnly)
    {
        var npc = new Npc { SteadingId = steadingId };
        npc.Update(name, description, gmNotes, gmOnly);
        return npc;
    }

    // Validates everything before changing anything, so a bad request leaves the NPC as it was.
    public void Update(string? name, string? description, string? gmNotes, bool gmOnly)
    {
        var trimmedName = name?.Trim() ?? "";
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException("A name is required.", nameof(name));
        }

        if (trimmedName.Length > NameMaxLength)
        {
            throw new ArgumentException($"The name can be at most {NameMaxLength} characters.", nameof(name));
        }

        description ??= "";
        if (description.Length > DescriptionMaxLength)
        {
            throw new ArgumentException(
                $"The description can be at most {DescriptionMaxLength} characters.",
                nameof(description));
        }

        gmNotes ??= "";
        if (gmNotes.Length > GmNotesMaxLength)
        {
            throw new ArgumentException($"GM notes can be at most {GmNotesMaxLength} characters.", nameof(gmNotes));
        }

        Name = trimmedName;
        Description = description;
        GmNotes = gmNotes;
        GmOnly = gmOnly;
    }

    public void Archive(DateTimeOffset archivedAt) => ArchivedAt ??= archivedAt;

    public void Restore() => ArchivedAt = null;
}
