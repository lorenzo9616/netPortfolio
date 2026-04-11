using System.Net;
using System.Net.Http.Json;
using OcrApi.Models;
using OcrApi.Tests.Helpers;
using Xunit;

namespace OcrApi.Tests;

public class OcrPropertiesControllerTests : IClassFixture<OcrWebAppFactory>
{
    private readonly OcrWebAppFactory _factory;

    public OcrPropertiesControllerTests(OcrWebAppFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetProperties_ReturnsOk()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/ocrproperties");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var props = await response.Content.ReadFromJsonAsync<List<OcrProperty>>();
        Assert.NotNull(props);
    }

    [Fact]
    public async Task CreateProperty_ValidDto_Returns201()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/ocrproperties", new
        {
            name = "TestField", dataType = "string", searchHeuristic = @"\d+", isActive = true
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<OcrProperty>();
        Assert.NotNull(created);
        Assert.Equal("TestField", created!.Name);
    }

    [Fact]
    public async Task CreateProperty_MissingName_Returns400()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.PostAsJsonAsync("/api/ocrproperties", new
        {
            dataType = "string", isActive = true
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProperty_ExistingId_Returns204()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var created = await client.PostAsJsonAsync("/api/ocrproperties", new
        {
            name = "ToDelete", dataType = "string", isActive = true
        });
        var prop = await created.Content.ReadFromJsonAsync<OcrProperty>();

        var response = await client.DeleteAsync($"/api/ocrproperties/{prop!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DeleteProperty_UnknownId_Returns404()
    {
        var client = await _factory.CreateAuthenticatedClientAsync();
        var response = await client.DeleteAsync("/api/ocrproperties/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProperties_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/ocrproperties");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
