using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Npcs;

public sealed class NpcService(SteadingDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<NpcResponse>> ListForGmAsync(CancellationToken cancellationToken = default)
    {
        var npcs = await db.Npcs.AsNoTracking().ToListAsync(cancellationToken);
        return SortByName(npcs).Select(NpcResponse.From).ToList();
    }

    // Returns null for an unknown token. The filter runs in the database query, so hidden
    // and archived NPCs never leave the server.
    public async Task<IReadOnlyList<PlayerNpcResponse>?> ListForPlayersAsync(
        string shareToken,
        CancellationToken cancellationToken = default)
    {
        var steadingId = await db.Steadings
            .Where(s => s.ShareToken == shareToken)
            .Select(s => (Guid?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (steadingId is null)
        {
            return null;
        }

        var npcs = await db.Npcs
            .AsNoTracking()
            .Where(n => n.SteadingId == steadingId && !n.GmOnly && n.ArchivedAt == null)
            .Select(n => new PlayerNpcResponse(n.Id, n.Name, n.Description))
            .ToListAsync(cancellationToken);
        return npcs.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ThenBy(n => n.Id).ToList();
    }

    // Throws ArgumentException for invalid fields.
    public async Task<NpcResponse> CreateAsync(NpcRequest request, CancellationToken cancellationToken = default)
    {
        var steadingId = await db.Steadings.Select(s => s.Id).SingleAsync(cancellationToken);
        var npc = Npc.Create(steadingId, request.Name, request.Description, request.GmNotes, request.GmOnly ?? false);
        db.Npcs.Add(npc);
        await db.SaveChangesAsync(cancellationToken);
        return NpcResponse.From(npc);
    }

    // Returns null for an unknown NPC; throws ArgumentException for invalid fields.
    public Task<NpcResponse?> UpdateAsync(Guid id, NpcRequest request, CancellationToken cancellationToken = default) =>
        ChangeAsync(
            id,
            npc => npc.Update(request.Name, request.Description, request.GmNotes, request.GmOnly ?? false),
            cancellationToken);

    public Task<NpcResponse?> ArchiveAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, npc => npc.Archive(timeProvider.GetUtcNow()), cancellationToken);

    public Task<NpcResponse?> RestoreAsync(Guid id, CancellationToken cancellationToken = default) =>
        ChangeAsync(id, npc => npc.Restore(), cancellationToken);

    private async Task<NpcResponse?> ChangeAsync(Guid id, Action<Npc> change, CancellationToken cancellationToken)
    {
        var npc = await db.Npcs.SingleOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (npc is null)
        {
            return null;
        }

        change(npc);
        await db.SaveChangesAsync(cancellationToken);
        return NpcResponse.From(npc);
    }

    // Sorted in memory so the order doesn't depend on the database's collation.
    private static IEnumerable<Npc> SortByName(IEnumerable<Npc> npcs) =>
        npcs.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ThenBy(n => n.Id);
}
