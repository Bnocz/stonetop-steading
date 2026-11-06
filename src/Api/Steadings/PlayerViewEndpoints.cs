using Microsoft.AspNetCore.Mvc;

namespace Api.Steadings;

public static class PlayerViewEndpoints
{
    public static IEndpointRouteBuilder MapPlayerViewEndpoints(this IEndpointRouteBuilder routes)
    {
        var view = routes.MapGroup("/api/view/{shareToken}").AddEndpointFilter(DoNotStoreResponse);
        view.MapGet("/steading", GetSteading);
        view.MapGet("/log", GetLog);
        return routes;
    }

    // Without this, a browser could keep showing the sheet after the GM replaces the link.
    private static async ValueTask<object?> DoNotStoreResponse(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        return await next(context);
    }

    // [FromServices] is explicit because SteadingService isn't registered without a database.
    private static async Task<IResult> GetSteading(
        string shareToken,
        [FromServices] SteadingService service,
        CancellationToken cancellationToken)
    {
        var steading = await service.GetByShareTokenAsync(shareToken, cancellationToken);
        return steading is null ? Results.NotFound() : Results.Ok(PlayerSteadingResponse.From(steading));
    }

    private static async Task<IResult> GetLog(
        string shareToken,
        [FromServices] SteadingService service,
        CancellationToken cancellationToken)
    {
        var steading = await service.GetByShareTokenAsync(shareToken, cancellationToken);
        if (steading is null)
        {
            return Results.NotFound();
        }

        var log = await service.GetLogAsync(steading.Id, cancellationToken);
        return Results.Ok(log.Select(PlayerLogEntryResponse.From));
    }
}
