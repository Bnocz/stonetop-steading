using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Auth;

internal static class GmApp
{
    public const string Username = "gm";
    public const string Password = "test-password";

    public static WebApplicationFactory<Program> Create(
        string? username = Username,
        string? password = Password,
        string? connectionString = null,
        string? environment = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            if (username is not null) builder.UseSetting("Admin:Username", username);
            if (password is not null) builder.UseSetting("Admin:Password", password);
            if (connectionString is not null) builder.UseSetting("ConnectionStrings:Default", connectionString);
            if (environment is not null) builder.UseEnvironment(environment);
        });

    public static Task<HttpResponseMessage> LoginAsync(
        this HttpClient client,
        string username = Username,
        string password = Password) =>
        client.PostAsJsonAsync("/api/gm/login", new { username, password });
}
