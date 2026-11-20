using Api.Domain;

namespace Api.Npcs;

public sealed record NpcRequest(string? Name, string? Description, string? GmNotes, bool? GmOnly);

public sealed record NpcResponse(
    Guid Id,
    string Name,
    string Description,
    string GmNotes,
    bool GmOnly,
    bool Archived,
    string? PortraitVersion)
{
    public static NpcResponse From(Npc npc, string? portraitVersion) =>
        new(npc.Id, npc.Name, npc.Description, npc.GmNotes, npc.GmOnly, npc.Archived, portraitVersion);
}

public sealed record PortraitResponse(string Version);

// Which visible NPCs have portraits. Kept out of PlayerNpcResponse, whose fields are fixed.
public sealed record PlayerPortraitResponse(Guid NpcId, string Version);

// Everything players can see about an NPC. It lists each field on purpose, so GM notes
// and new columns can't reach players unless they're added here.
public sealed record PlayerNpcResponse(Guid Id, string Name, string Description);
