using Api.Health;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// The built React app is copied into wwwroot by the Dockerfile.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthEndpoints();

app.MapFallbackToFile("index.html");

app.Run();
