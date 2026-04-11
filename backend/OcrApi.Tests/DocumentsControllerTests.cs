using System.Net;
using System.Net.Http.Json;
using OcrApi.Tests.Helpers;
using Xunit;

namespace OcrApi.Tests;

public class DocumentsControllerTests : IClassFixture<OcrWebAppFactory>
{
    private readonly OcrWebAppFactory _factory;

    public DocumentsControllerTests(OcrWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ListDocuments_ReturnsOkWithPaginationMetadata()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/documents?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.NotNull(body);
        Assert.True(body!.ContainsKey("items"));
        Assert.True(body.ContainsKey("totalCount"));
        Assert.True(body.ContainsKey("page"));
        Assert.True(body.ContainsKey("pageSize"));
    }

    [Fact]
    public async Task ListDocuments_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetById_UnknownId_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/documents/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteDocument_UnknownId_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.DeleteAsync("/api/documents/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
