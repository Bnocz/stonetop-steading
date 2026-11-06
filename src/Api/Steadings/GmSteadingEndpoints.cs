using Api.Domain;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Steadings;

public static class GmSteadingEndpoints
{
    public static IEndpointRouteBuilder MapGmSteadingEndpoints(this IEndpointRouteBuilder routes)
    {
        var steading = routes.MapGroup("/api/gm/steading").RequireAuthorization();
        steading.MapGet("/", GetSteading);
        steading.MapPut("/stats/{stat}", ChangeStat);
        steading.MapGet("/log", GetLog);
        return routes;
    }

    // [FromServices] is explicit because SteadingService isn't registered without a database,
    // and minimal APIs would otherwise fail at startup trying to infer where it comes from.
    private static async Task<IResult> GetSteading(
        [FromServices] SteadingService service,
        CancellationToken cancellationToken)
    {
        var steading = await service.GetCurrentAsync(cancellationToken);
        return steading is null ? Results.NotFound() : Results.Ok(SteadingResponse.From(steading));
    }

    private static async Task<IResult> ChangeStat(
        string stat,
        ChangeStatRequest request,
        [FromServices] SteadingService service,
        CancellationToken cancellationToken)
    {
        if (!TryParseStat(stat, out var parsedStat))
        {
            return Results.NotFound();
        }

        if (request.Value is not { } value)
        {
            return Results.Problem(detail: "A value is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var steading = await service.GetCurrentAsync(cancellationToken);
        if (steading is null)
        {
            return Results.NotFound();
        }

        try
        {
            await service.ChangeStatAsync(steading.Id, parsedStat, value, cancellationToken);
        }
        catch (ArgumentOutOfRangeException)
        {
            return Results.Problem(
                detail: $"{value} is out of range for {parsedStat}.",
                statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Problem(
                detail: "The steading changed while saving. Reload and try again.",
                statusCode: StatusCodes.Status409Conflict);
        }

        return Results.Ok(SteadingResponse.From(steading));
    }

    private static async Task<IResult> GetLog(
        [FromServices] SteadingService service,
        CancellationToken cancellationToken)
    {
        var steading = await service.GetCurrentAsync(cancellationToken);
        return steading is null
            ? Results.NotFound()
            : Results.Ok(await service.GetLogAsync(steading.Id, cancellationToken));
    }

    // Matches names only: Enum.TryParse alone would also accept numbers like "3".
    private static bool TryParseStat(string value, out SteadingStat stat) =>
        Enum.TryParse(value, ignoreCase: true, out stat)
        && Enum.GetNames<SteadingStat>().Contains(value, StringComparer.OrdinalIgnoreCase);
}
