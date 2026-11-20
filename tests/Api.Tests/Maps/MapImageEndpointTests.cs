using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Api.Tests.Auth;
using Api.Tests.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Api.Tests.Maps;

[Collection(PostgresCollection.Name)]
public sealed class MapImageEndpointTests(PostgresFixture postgres)
{
    private sealed record ImageBody(string Version, string ContentType);

    private sealed record MapBody(ImageBody? Image);

    // A real 1x1 PNG.
    internal static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    // Only the signatures matter to the server, which never decodes the image.
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, .. "JFIF"u8, 0x00, 0x01, 0x02];

    private static readonly byte[] WebP = [.. "RIFF"u8, 0x1A, 0x00, 0x00, 0x00, .. "WEBPVP8 "u8, 0x0E, 0x00, 0x00, 0x00];

    private async Task<(WebApplicationFactory<Program> App, HttpClient Client)> StartSignedInAsync()
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        var app = GmApp.Create(connectionString: connectionString);
        var client = app.CreateClient();
        await client.LoginAsync();
        return (app, client);
    }

    internal static Task<HttpResponseMessage> UploadAsync(HttpClient client, byte[] bytes, string contentType = "image/png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        return client.PutAsync("/api/gm/map/image", content);
    }

    [Theory]
    [InlineData("GET", "/api/gm/map")]
    [InlineData("GET", "/api/gm/map/image")]
    [InlineData("PUT", "/api/gm/map/image")]
    public async Task Endpoints_require_sign_in(string method, string url)
    {
        var connectionString = await postgres.CreateDatabaseAsync();
        await using var app = GmApp.Create(connectionString: connectionString);
        using var client = app.CreateClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "PUT")
        {
            request.Content = new ByteArrayContent(Png);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        }

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task There_is_no_map_until_one_is_uploaded()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            Assert.Null((await client.GetFromJsonAsync<MapBody>("/api/gm/map"))!.Image);
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/gm/map/image")).StatusCode);
        }
    }

    [Fact]
    public async Task Uploaded_png_is_served_back_unchanged()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var upload = await UploadAsync(client, Png);

            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            var uploaded = (await upload.Content.ReadFromJsonAsync<ImageBody>())!;
            Assert.Equal("image/png", uploaded.ContentType);
            Assert.False(string.IsNullOrWhiteSpace(uploaded.Version));
            Assert.Equal(uploaded, (await client.GetFromJsonAsync<MapBody>("/api/gm/map"))!.Image);

            var image = await client.GetAsync("/api/gm/map/image");

            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
            Assert.Equal(Png, await image.Content.ReadAsByteArrayAsync());
        }
    }

    [Theory]
    [InlineData("jpeg", "image/jpeg")]
    [InlineData("webp", "image/webp")]
    public async Task Jpeg_and_webp_are_accepted(string kind, string expectedType)
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var bytes = kind == "jpeg" ? Jpeg : WebP;

            var upload = await UploadAsync(client, bytes, expectedType);

            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            Assert.Equal(expectedType, (await upload.Content.ReadFromJsonAsync<ImageBody>())!.ContentType);
            Assert.Equal(bytes, await client.GetByteArrayAsync("/api/gm/map/image"));
        }
    }

    [Fact]
    public async Task The_type_comes_from_the_file_contents_not_the_header()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var upload = await UploadAsync(client, Png, "image/jpeg");

            Assert.Equal(HttpStatusCode.OK, upload.StatusCode);
            var image = await client.GetAsync("/api/gm/map/image");
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        }
    }

    public static TheoryData<string, byte[]> NotImages => new()
    {
        { "empty", [] },
        { "text", "<svg xmlns='http://www.w3.org/2000/svg'></svg>"u8.ToArray() },
        { "gif", "GIF89a\u0001\u0000\u0001\u0000"u8.ToArray() },
        { "truncated png", Png[..4] },
    };

    [Theory]
    [MemberData(nameof(NotImages))]
    public async Task Files_that_are_not_png_jpeg_or_webp_are_rejected(string kind, byte[] bytes)
    {
        _ = kind;
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var upload = await UploadAsync(client, bytes);

            Assert.Equal(HttpStatusCode.BadRequest, upload.StatusCode);
            Assert.Null((await client.GetFromJsonAsync<MapBody>("/api/gm/map"))!.Image);
        }
    }

    [Fact]
    public async Task Files_over_10_mb_are_rejected()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var bytes = new byte[(10 * 1024 * 1024) + 1];
            Png.CopyTo(bytes, 0);

            var upload = await UploadAsync(client, bytes);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, upload.StatusCode);
            Assert.Null((await client.GetFromJsonAsync<MapBody>("/api/gm/map"))!.Image);
        }
    }

    [Fact]
    public async Task A_file_of_exactly_10_mb_is_accepted()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var bytes = new byte[10 * 1024 * 1024];
            Png.CopyTo(bytes, 0);

            Assert.Equal(HttpStatusCode.OK, (await UploadAsync(client, bytes)).StatusCode);
        }
    }

    [Fact]
    public async Task Replacing_the_map_serves_the_new_file_with_a_new_version()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            var first = (await (await UploadAsync(client, Png)).Content.ReadFromJsonAsync<ImageBody>())!;

            var second = (await (await UploadAsync(client, WebP)).Content.ReadFromJsonAsync<ImageBody>())!;

            Assert.NotEqual(first.Version, second.Version);
            Assert.Equal(WebP, await client.GetByteArrayAsync("/api/gm/map/image"));
        }
    }

    [Fact]
    public async Task Image_supports_conditional_requests()
    {
        var (app, client) = await StartSignedInAsync();
        await using (app)
        using (client)
        {
            await UploadAsync(client, Png);
            var first = await client.GetAsync("/api/gm/map/image");
            var etag = first.Headers.ETag;
            Assert.NotNull(etag);

            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/gm/map/image");
            request.Headers.IfNoneMatch.Add(etag);
            var second = await client.SendAsync(request);

            Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
        }
    }
}
