using Api.Data;
using Api.Health;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSteadingDatabase(builder.Configuration);

var app = builder.Build();

// The built React app is copied into wwwroot by the Dockerfile.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthEndpoints();

app.MapFallbackToFile("index.html");

await app.InitializeDatabaseAsync();

app.Run();
