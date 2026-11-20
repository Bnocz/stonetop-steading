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
