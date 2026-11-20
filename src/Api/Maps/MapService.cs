using Api.Data;
using Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Api.Maps;

// Thrown when another session already has the requested number.
public sealed class DuplicateSessionNumberException(int number)
    : Exception($"There is already a session {number}.");

public sealed class MapService(SteadingDbContext db, TimeProvider timeProvider)
{
    public async Task<MapImageResponse?> GetImageInfoAsync(CancellationToken cancellationToken = default) =>
        await db.MapImages
            .Select(i => new MapImageResponse(i.Version, i.ContentType))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<MapImage?> GetImageAsync(CancellationToken cancellationToken = default) =>
        db.MapImages.AsNoTracking().SingleOrDefaultAsync(cancellationToken);

    // Throws ArgumentException for files that aren't a PNG, JPEG or WebP within the size limit.
    public async Task<MapImageResponse> UploadImageAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        var image = await db.MapImages.SingleOrDefaultAsync(cancellationToken);
        if (image is null)
        {
            var steadingId = await db.Steadings.Select(s => s.Id).SingleAsync(cancellationToken);
            image = MapImage.Create(steadingId, data, now);
            db.MapImages.Add(image);
        }
        else
        {
            image.Replace(data, now);
        }

        await db.SaveChangesAsync(cancellationToken);
        return MapImageResponse.From(image);
    }

    public async Task<IReadOnlyList<SessionResponse>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await db.PlaySessions.AsNoTracking().OrderBy(s => s.Number).ToListAsync(cancellationToken);
        return sessions.Select(SessionResponse.From).ToList();
    }

    // Throws ArgumentException for invalid fields and DuplicateSessionNumberException for a taken number.
    public async Task<SessionResponse> CreateSessionAsync(SessionRequest request, CancellationToken cancellationToken = default)
    {
        var steadingId = await db.Steadings.Select(s => s.Id).SingleAsync(cancellationToken);
        var session = PlaySession.Create(steadingId, request.Number, request.Title, request.GmOnly ?? true);
        db.PlaySessions.Add(session);
        await SaveAsync(session.Number, cancellationToken);
        return SessionResponse.From(session);
    }

    // Returns null for an unknown session.
    public async Task<SessionResponse?> UpdateSessionAsync(
        Guid id,
        SessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var session = await db.PlaySessions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return null;
        }

        session.Update(request.Number, request.Title, request.GmOnly);
        await SaveAsync(session.Number, cancellationToken);
        return SessionResponse.From(session);
    }

    // Returns null for an unknown session. Every waypoint is validated before the route changes.
    public async Task<SessionResponse?> ReplaceRouteAsync(
        Guid id,
        IReadOnlyList<WaypointRequest> waypoints,
        CancellationToken cancellationToken = default)
    {
        var session = await db.PlaySessions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (session is null)
        {
            return null;
        }

        session.ReplaceRoute(waypoints.Select(w => Waypoint.Create(w.X, w.Y, w.Label)).ToList());
        await db.SaveChangesAsync(cancellationToken);
        return SessionResponse.From(session);
    }

    public async Task<bool> DeleteSessionAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.PlaySessions.Where(s => s.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;

    // The unique index catches two saves racing for the same number.
    private async Task SaveAsync(int number, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new DuplicateSessionNumberException(number);
        }
    }
}
