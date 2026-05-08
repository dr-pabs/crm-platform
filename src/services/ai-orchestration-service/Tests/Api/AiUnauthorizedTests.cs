using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.AiOrchestrationService.Tests.Api;

/// <summary>
/// Verifies every AI Orchestration endpoint returns HTTP 401 when no Authorization header is supplied.
/// CLAUDE.md: every endpoint requires a 401 unauthorised test (no token).
/// </summary>
public sealed class AiUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── AI Jobs ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task POST_EnqueueAiJob_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/ai/jobs", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_AiJob_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/ai/jobs/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_AiResult_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/ai/results/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Prompt Templates ──────────────────────────────────────────────────────

    [Fact]
    public async Task GET_PromptTemplates_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/ai/prompt-templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_PromptTemplate_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/ai/prompt-templates", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PUT_PromptTemplate_NoToken_Returns401()
    {
        var response = await _client.PutAsync($"/ai/prompt-templates/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task DELETE_PromptTemplate_NoToken_Returns401()
    {
        var response = await _client.DeleteAsync($"/ai/prompt-templates/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── SMS Records ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_SmsRecords_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/ai/sms-records");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
