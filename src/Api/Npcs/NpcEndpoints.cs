using Microsoft.AspNetCore.Mvc;

namespace Api.Npcs;

public static class NpcEndpoints
{
    public static IEndpointRouteBuilder MapNpcEndpoints(this IEndpointRouteBuilder routes)
    {
        var gm = routes.MapGroup("/api/gm/npcs").RequireAuthorization();
        gm.MapGet("/", List);
        gm.MapPost("/", Create);
        gm.MapPut("/{id:guid}", Update);
        gm.MapDelete("/{id:guid}", Archive);
        gm.MapPost("/{id:guid}/restore", Restore);

        routes.MapGet("/api/view/{shareToken}/npcs", ListForPlayers);
        return routes;
    }

    // [FromServices] is explicit because NpcService isn't registered without a database.
    private static async Task<IResult> List([FromServices] NpcService service, CancellationToken cancellationToken) =>
        Results.Ok(await service.ListForGmAsync(cancellationToken));

    private static async Task<IResult> Create(
        NpcRequest request,
        [FromServices] NpcService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var created = await service.CreateAsync(request, cancellationToken);
            return Results.Created($"/api/gm/npcs/{created.Id}", created);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception);
        }
    }

    private static async Task<IResult> Update(
        Guid id,
        NpcRequest request,
        [FromServices] NpcService service,
        CancellationToken cancellationToken)
    {
        try
        {
            return OkOrNotFound(await service.UpdateAsync(id, request, cancellationToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception);
        }
    }

    private static async Task<IResult> Archive(
        Guid id,
        [FromServices] NpcService service,
        CancellationToken cancellationToken) =>
        OkOrNotFound(await service.ArchiveAsync(id, cancellationToken));

    private static async Task<IResult> Restore(
        Guid id,
        [FromServices] NpcService service,
        CancellationToken cancellationToken) =>
        OkOrNotFound(await service.RestoreAsync(id, cancellationToken));

    private static async Task<IResult> ListForPlayers(
        string shareToken,
        HttpContext context,
        [FromServices] NpcService service,
        CancellationToken cancellationToken)
    {
        // Without this, a browser could keep showing NPCs after the GM hides them or replaces the link.
        context.Response.Headers.CacheControl = "no-store";
        var npcs = await service.ListForPlayersAsync(shareToken, cancellationToken);
        return npcs is null ? Results.NotFound() : Results.Ok(npcs);
    }

    private static IResult OkOrNotFound(NpcResponse? npc) => npc is null ? Results.NotFound() : Results.Ok(npc);

    private static IResult BadRequest(ArgumentException exception) =>
        Results.Problem(detail: StripParameterName(exception), statusCode: StatusCodes.Status400BadRequest);

    // ArgumentException appends " (Parameter 'name')" to Message; the client only needs the sentence.
    private static string StripParameterName(ArgumentException exception) =>
        exception.ParamName is null
            ? exception.Message
            : exception.Message.Replace($" (Parameter '{exception.ParamName}')", "");
}
