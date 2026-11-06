using Api.Auth;
using Api.Data;
using Api.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSteadingDatabase(builder.Configuration);
builder.Services.AddGmAuth(builder.Environment);

var app = builder.Build();

// The built React app is copied into wwwroot by the Dockerfile.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseGmAuth();

app.MapHealthEndpoints();
app.MapAuthEndpoints();

// Unknown API routes are errors, not pages for the React app to handle.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.InitializeDatabaseAsync();

app.Run();
