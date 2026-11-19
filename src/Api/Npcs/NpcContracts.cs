using Api.Domain;

namespace Api.Npcs;

public sealed record NpcRequest(string? Name, string? Description, string? GmNotes, bool? GmOnly);

public sealed record NpcResponse(Guid Id, string Name, string Description, string GmNotes, bool GmOnly, bool Archived)
{
    public static NpcResponse From(Npc npc) =>
        new(npc.Id, npc.Name, npc.Description, npc.GmNotes, npc.GmOnly, npc.Archived);
}

// Everything players can see about an NPC. It lists each field on purpose, so GM notes
// and new columns can't reach players unless they're added here.
public sealed record PlayerNpcResponse(Guid Id, string Name, string Description);
