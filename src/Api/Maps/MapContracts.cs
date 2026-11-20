using Api.Domain;

namespace Api.Maps;

public sealed record MapImageResponse(string Version, string ContentType)
{
    public static MapImageResponse From(MapImage image) => new(image.Version, image.ContentType);
}

public sealed record MapResponse(MapImageResponse? Image);

public sealed record SessionRequest(int? Number, string? Title, bool? GmOnly);

public sealed record WaypointRequest(double? X, double? Y, string? Label);

public sealed record RouteRequest(IReadOnlyList<WaypointRequest>? Waypoints);

public sealed record WaypointResponse(double X, double Y, string Label);

// Everything players can see about a session. It lists each field on purpose, so GmOnly
// and new columns can't reach players unless they're added here.
public sealed record PlayerSessionResponse(Guid Id, int Number, string Title, IReadOnlyList<WaypointResponse> Waypoints)
{
    public static PlayerSessionResponse From(PlaySession session) =>
        new(
            session.Id,
            session.Number,
            session.Title,
            session.Waypoints.Select(w => new WaypointResponse(w.X, w.Y, w.Label)).ToList());
}

public sealed record SessionResponse(
    Guid Id,
    int Number,
    string Title,
    bool GmOnly,
    IReadOnlyList<WaypointResponse> Waypoints)
{
    public static SessionResponse From(PlaySession session) =>
        new(
            session.Id,
            session.Number,
            session.Title,
            session.GmOnly,
            session.Waypoints.Select(w => new WaypointResponse(w.X, w.Y, w.Label)).ToList());
}
