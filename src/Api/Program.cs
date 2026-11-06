using Api.Auth;
using Api.Data;
using Api.Health;
using Api.Steadings;
using Api.Web;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSteadingDatabase(builder.Configuration);
builder.Services.AddGmAuth(builder.Environment);
builder.Services.ConfigureHttpJsonOptions(
    options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

// The built React app is copied into wwwroot by the Dockerfile.
app.UseDefaultFiles();
app.UseStaticFiles(StaticFileCaching.Options());

app.UseGmAuth();

app.MapHealthEndpoints();
app.MapAuthEndpoints();
app.MapGmSteadingEndpoints();

// Unknown API routes are errors, not pages for the React app to handle.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html", StaticFileCaching.Options());

await app.InitializeDatabaseAsync();

app.Run();
