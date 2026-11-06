using Microsoft.Net.Http.Headers;

namespace Api.Web;

public static class StaticFileCaching
{
    // index.html names the current build's assets, so browsers must check for a new one after a
    // deploy. The assets have a content hash in their file names and can stay cached.
    public static StaticFileOptions Options() => new()
    {
        OnPrepareResponse = context =>
        {
            if (context.File.Name == "index.html")
            {
                context.Context.Response.Headers[HeaderNames.CacheControl] = "no-cache";
            }
        },
    };
}
