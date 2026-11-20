using Api.Domain;
using Api.Images;
using Microsoft.AspNetCore.Mvc;

namespace Api.Maps;

public static class MapEndpoints
{
    public static IEndpointRouteBuilder MapMapEndpoints(this IEndpointRouteBuilder routes)
    {
        var map = routes.MapGroup("/api/gm/map").RequireAuthorization();
        map.MapGet("/", GetMap);
        map.MapGet("/image", GetImage);
        map.MapPut("/image", UploadImage);

        var sessions = routes.MapGroup("/api/gm/sessions").RequireAuthorization();
        sessions.MapGet("/", ListSessions);
        sessions.MapPost("/", CreateSession);
        sessions.MapPut("/{id:guid}", UpdateSession);
        sessions.MapPut("/{id:guid}/waypoints", ReplaceRoute);
        sessions.MapDelete("/{id:guid}", DeleteSession);

        var view = routes.MapGroup("/api/view/{shareToken}");
        view.MapGet("/map", GetPlayerMap);
        view.MapGet("/map/image", GetPlayerImage);
        view.MapGet("/sessions", ListPlayerSessions);
        return routes;
    }

    // [FromServices] is explicit because MapService isn't registered without a database.
    private static async Task<IResult> GetMap([FromServices] MapService service, CancellationToken cancellationToken) =>
        Results.Ok(new MapResponse(await service.GetImageInfoAsync(cancellationToken)));

    private static async Task<IResult> GetImage(
        HttpContext context,
        [FromServices] MapService service,
        CancellationToken cancellationToken)
    {
        var image = await service.GetImageAsync(cancellationToken);
        if (image is null)
        {
            return Results.NotFound();
        }

        return ImageResult(context, image);
    }

    // The body is the raw image file. Its type is checked from its bytes, not the Content-Type header.
    private static async Task<IResult> UploadImage(
        HttpRequest request,
        [FromServices] MapService service,
        CancellationToken cancellationToken)
    {
        var data = await ImageHttp.ReadBodyAsync(request, MapImage.MaxBytes, cancellationToken);
        if (data is null)
        {
            return ImageHttp.TooLarge(MapImage.MaxBytes, "map");
        }

        try
        {
            return Results.Ok(await service.UploadImageAsync(data, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception);
        }
    }

    private static async Task<IResult> GetPlayerMap(
        string shareToken,
        HttpContext context,
        [FromServices] MapService service,
        CancellationToken cancellationToken)
    {
        DoNotStore(context);
        var steadingId = await service.FindSteadingByShareTokenAsync(shareToken, cancellationToken);
        return steadingId is { } id
            ? Results.Ok(new MapResponse(await service.GetImageInfoAsync(id, cancellationToken)))
            : Results.NotFound();
    }

    private static async Task<IResult> GetPlayerImage(
        string shareToken,
        HttpContext context,
        [FromServices] MapService service,
        CancellationToken cancellationToken)
    {
        var steadingId = await service.FindSteadingByShareTokenAsync(shareToken, cancellationToken);
        var image = steadingId is { } id ? await service.GetImageAsync(id, cancellationToken) : null;
        return image is null ? Results.NotFound() : ImageResult(context, image);
    }

    private static async Task<IResult> ListPlayerSessions(
        string shareToken,
        HttpContext context,
        [FromServices] MapService service,
        CancellationToken cancellationToken)
    {
        DoNotStore(context);
        var steadingId = await service.FindSteadingByShareTokenAsync(shareToken, cancellationToken);
        return steadingId is { } id
            ? Results.Ok(await service.ListPlayerSessionsAsync(id, cancellationToken))
            : Results.NotFound();
    }

    // Without this, a browser could keep showing a session after the GM hides it or replaces the link.
    private static void DoNotStore(HttpContext context) => context.Response.Headers.CacheControl = "no-store";

    private static async Task<IResult> ListSessions([FromServices] MapService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListSessionsAsync(cancellationToken));

    private static async Task<IResult> CreateSession(
        SessionRequest request,
        [FromServices] MapService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateSessionAsync(request, cancellationToken);
            return Results.Created($"/api/gm/sessions/{created.Id}", created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception);
        }
        catch (DuplicateSessionNumberException exception)
        {
            return Conflict(exception);
        }
    }

    private static async Task<IResult> UpdateSession(
        Guid id,
        SessionRequest request,
        [FromServices] MapService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var updated = await service.UpdateSessionAsync(id, request, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception);
        }
        catch (DuplicateSessionNumberException exception)
        {
            return Conflict(exception);
        }
    }

    private static async Task<IResult> ReplaceRoute(
        Guid id,
        RouteRequest request,
        [FromServices] MapService service,
        CancellationToken cancellationToken)
    {
        if (request.Waypoints is null)
        {
            return Results.Problem(detail: "A list of waypoints is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var updated = await service.ReplaceRouteAsync(id, request.Waypoints, cancellationToken);
            return updated is null ? Results.NotFound() : Results.Ok(updated);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception);
        }
    }

    private static async Task<IResult> DeleteSession(
        Guid id,
        [FromServices] MapService service,
        CancellationToken cancellationToken) =>
        await service.DeleteSessionAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound();

    private static IResult ImageResult(HttpContext context, MapImage image) =>
        ImageHttp.Serve(context, image.Data, image.ContentType, image.Version, image.UploadedAt);

    private static IResult BadRequest(ArgumentException exception) =>
        Results.Problem(detail: StripParameterName(exception), statusCode: StatusCodes.Status400BadRequest);

    private static IResult Conflict(DuplicateSessionNumberException exception) =>
        Results.Problem(detail: exception.Message, statusCode: StatusCodes.Status409Conflict);

    // ArgumentException appends " (Parameter 'name')" to Message; the client only needs the sentence.
    private static string StripParameterName(ArgumentException exception) =>
        exception.ParamName is null
            ? exception.Message
            : exception.Message.Replace($" (Parameter '{exception.ParamName}')", "");
}
