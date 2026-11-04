namespace Api.Health;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/health", GetHealth);
        return routes;
    }

    // Liveness only: reports that the process is up, with no database dependency.
    private static HealthResponse GetHealth() => new("ok");
}
