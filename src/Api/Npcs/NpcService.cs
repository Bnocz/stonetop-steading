using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Npcs;

public sealed class NpcService(SteadingDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<NpcResponse>> ListForGmAsync(CancellationToken cancellationToken = default)
    {
        var npcs = await db.Npcs.AsNoTracking().ToListAsync(cancellationToken);
        var portraits = await db.NpcPortraits.ToDictionaryAsync(p => p.NpcId, p => p.Version, cancellationToken);
        return SortByName(npcs).Select(n => NpcResponse.From(n, portraits.GetValueOrDefault(n.Id))).ToList();
    }

    // Returns null for an unknown token. Only visible NPCs' portraits are listed.
    public async Task<IReadOnlyList<PlayerPortraitResponse>?> ListPlayerPortraitsAsync(
        string shareToken,
        CancellationToken cancellationToken = default)
    {
        var steadingId = await FindSteadingAsync(shareToken, cancellationToken);
        if (steadingId is null)
        {
            return null;
        }

        return await db.NpcPortraits
            .Where(p => db.Npcs.Any(n => n.Id == p.NpcId && n.SteadingId == steadingId && !n.GmOnly && n.ArchivedAt == null))
            .Select(p => new PlayerPortraitResponse(p.NpcId, p.Version))
            .ToListAsync(cancellationToken);
    }

    // Returns null unless the token is current and the NPC is visible to players.
    public async Task<NpcPortrait?> GetPlayerPortraitAsync(
        string shareToken,
        Guid npcId,
        CancellationToken cancellationToken = default)
    {
        var steadingId = await FindSteadingAsync(shareToken, cancellationToken);
        if (steadingId is null)
        {
            return null;
        }

        return await db.NpcPortraits
            .AsNoTracking()
            .Where(p => p.NpcId == npcId)
            .Where(p => db.Npcs.Any(n => n.Id == p.NpcId && n.SteadingId == steadingId && !n.GmOnly && n.ArchivedAt == null))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public Task<NpcPortrait?> GetPortraitAsync(Guid npcId, CancellationToken cancellationToken = default) =>
        db.NpcPortraits.AsNoTracking().SingleOrDefaultAsync(p => p.NpcId == npcId, cancellationToken);

    // Returns null for an unknown NPC; throws ArgumentException for files that aren't a small PNG, JPEG or WebP.
    public async Task<PortraitResponse?> UploadPortraitAsync(
        Guid npcId,
        byte[] data,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Npcs.AnyAsync(n => n.Id == npcId, cancellationToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var portrait = await db.NpcPortraits.SingleOrDefaultAsync(p => p.NpcId == npcId, cancellationToken);
        if (portrait is null)
        {
            portrait = NpcPortrait.Create(npcId, data, now);
            db.NpcPortraits.Add(portrait);
        }
        else
        {
            portrait.Replace(data, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return new PortraitResponse(portrait.Version);
    }

    // Returns false for an unknown NPC. Removing a portrait that isn't there is fine.
    public async Task<bool> RemovePortraitAsync(Guid npcId, CancellationToken cancellationToken = default)
    {
        if (!await db.Npcs.AnyAsync(n => n.Id == npcId, cancellationToken))
        {
            return false;
        }

        await db.NpcPortraits.Where(p => p.NpcId == npcId).ExecuteDeleteAsync(cancellationToken);
        return true;
    }

    private Task<Guid?> FindSteadingAsync(string shareToken, CancellationToken cancellationToken) =>
        db.Steadings
            .Where(s => s.ShareToken == shareToken)
            .Select(s => (Guid?)s.Id)
            .SingleOrDefaultAsync(cancellationToken);

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
        return NpcResponse.From(npc, portraitVersion: null);
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
        var portraitVersion = await db.NpcPortraits
            .Where(p => p.NpcId == id)
            .Select(p => p.Version)
            .SingleOrDefaultAsync(cancellationToken);
        return NpcResponse.From(npc, portraitVersion);
    }

    // Sorted in memory so the order doesn't depend on the database's collation.
    private static IEnumerable<Npc> SortByName(IEnumerable<Npc> npcs) =>
        npcs.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ThenBy(n => n.Id);
}
