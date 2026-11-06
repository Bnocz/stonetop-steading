using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var gm = routes.MapGroup("/api/gm");
        gm.MapPost("/login", Login).RequireRateLimiting(AuthSetup.LoginRateLimitPolicy);
        gm.MapPost("/logout", Logout).RequireAuthorization();
        gm.MapGet("/me", Me).RequireAuthorization();
        return routes;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        [FromServices] GmCredentials credentials,
        HttpContext context)
    {
        if (!credentials.Verify(request.Username, request.Password))
        {
            return Results.Unauthorized();
        }

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, credentials.Username!)],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });
        return Results.Ok(new GmUserResponse(credentials.Username!));
    }

    private static async Task Logout(HttpContext context)
    {
        await context.SignOutAsync();
        context.Response.StatusCode = StatusCodes.Status204NoContent;
    }

    private static GmUserResponse Me(ClaimsPrincipal user) => new(user.Identity!.Name!);
}
