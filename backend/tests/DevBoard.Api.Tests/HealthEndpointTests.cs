using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace DevBoard.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<TestingApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(TestingApplicationFactory application)
    {
        _client = application.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsHealthyApiStatus()
    {
        var response = await _client.GetAsync("/api/health");
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("healthy", body.Status);
        Assert.Equal("DevBoard.Api", body.Service);
        Assert.True(body.CheckedAtUtc <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task CreateProject_WithShortName_ReturnsValidationProblem()
    {
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = "ab",
            description = "This should not be saved."
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateProject_WithValidData_ReturnsCreatedProject()
    {
        var uniqueName = $"Test project {Guid.NewGuid()}";
        var response = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = uniqueName,
            description = "Created by an integration test."
        });
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(project);
        Assert.Equal(uniqueName, project.Name);
    }

    [Fact]
    public async Task UpdateProject_WithValidData_ReturnsUpdatedProject()
    {
        var createResponse = await _client.PostAsJsonAsync("/api/projects", new
        {
            name = $"Before update {Guid.NewGuid()}",
            description = "Original description."
        });
        var created = await createResponse.Content.ReadFromJsonAsync<ProjectResponse>();
        Assert.NotNull(created);

        var updateResponse = await _client.PutAsJsonAsync($"/api/projects/{created.Id}", new
        {
            name = $"After update {Guid.NewGuid()}",
            description = "Updated description."
        });
        var updated = await updateResponse.Content.ReadFromJsonAsync<ProjectResponse>();

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        Assert.NotNull(updated);
        Assert.Equal(created.Id, updated.Id);
        Assert.Equal("Updated description.", updated.Description);
    }

    private sealed record ProjectResponse(Guid Id, string Name, string Description, DateTimeOffset CreatedAtUtc);
}

public sealed class TestingApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DevBoard", "Data Source=devboard-test.db");
    }
}
