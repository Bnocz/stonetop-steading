using Api.Domain;
using Api.Images;
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
        gm.MapGet("/{id:guid}/portrait", GetPortrait);
        gm.MapPut("/{id:guid}/portrait", UploadPortrait);
        gm.MapDelete("/{id:guid}/portrait", RemovePortrait);

        routes.MapGet("/api/view/{shareToken}/npcs", ListForPlayers);
        routes.MapGet("/api/view/{shareToken}/npc-portraits", ListPlayerPortraits);
        routes.MapGet("/api/view/{shareToken}/npcs/{id:guid}/portrait", GetPlayerPortrait);
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

    private static async Task<IResult> GetPortrait(
        Guid id,
        HttpContext context,
        [FromServices] NpcService service,
        CancellationToken cancellationToken)
    {
        var portrait = await service.GetPortraitAsync(id, cancellationToken);
        return portrait is null ? Results.NotFound() : Serve(context, portrait);
    }

    // The body is the raw image file. Its type is checked from its bytes, not the Content-Type header.
    private static async Task<IResult> UploadPortrait(
        Guid id,
        HttpRequest request,
        [FromServices] NpcService service,
        CancellationToken cancellationToken)
    {
        var data = await ImageHttp.ReadBodyAsync(request, NpcPortrait.MaxBytes, cancellationToken);
        if (data is null)
        {
            return ImageHttp.TooLarge(NpcPortrait.MaxBytes, "portrait");
        }

        try
        {
            var uploaded = await service.UploadPortraitAsync(id, data, cancellationToken);
            return uploaded is null ? Results.NotFound() : Results.Ok(uploaded);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(exception);
        }
    }

    private static async Task<IResult> RemovePortrait(
        Guid id,
        [FromServices] NpcService service,
        CancellationToken cancellationToken) =>
        await service.RemovePortraitAsync(id, cancellationToken) ? Results.NoContent() : Results.NotFound();

    private static async Task<IResult> ListPlayerPortraits(
        string shareToken,
        HttpContext context,
        [FromServices] NpcService service,
        CancellationToken cancellationToken)
    {
        // Without this, a browser could keep showing a portrait list after the GM hides an NPC.
        context.Response.Headers.CacheControl = "no-store";
        var portraits = await service.ListPlayerPortraitsAsync(shareToken, cancellationToken);
        return portraits is null ? Results.NotFound() : Results.Ok(portraits);
    }

    private static async Task<IResult> GetPlayerPortrait(
        string shareToken,
        Guid id,
        HttpContext context,
        [FromServices] NpcService service,
        CancellationToken cancellationToken)
    {
        var portrait = await service.GetPlayerPortraitAsync(shareToken, id, cancellationToken);
        return portrait is null ? Results.NotFound() : Serve(context, portrait);
    }

    private static IResult Serve(HttpContext context, NpcPortrait portrait) =>
        ImageHttp.Serve(context, portrait.Data, portrait.ContentType, portrait.Version, portrait.UploadedAt);

    private static IResult OkOrNotFound(NpcResponse? npc) => npc is null ? Results.NotFound() : Results.Ok(npc);

    private static IResult BadRequest(ArgumentException exception) =>
        Results.Problem(detail: StripParameterName(exception), statusCode: StatusCodes.Status400BadRequest);

    // ArgumentException appends " (Parameter 'name')" to Message; the client only needs the sentence.
    private static string StripParameterName(ArgumentException exception) =>
        exception.ParamName is null
            ? exception.Message
            : exception.Message.Replace($" (Parameter '{exception.ParamName}')", "");
}
