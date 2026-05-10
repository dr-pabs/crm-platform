using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace CrmPlatform.NotificationService.Tests.Api;

/// <summary>
/// Verifies every Notification endpoint returns HTTP 401 when no Authorization header is supplied.
/// CLAUDE.md: every endpoint requires a 401 unauthorised test (no token).
/// </summary>
public sealed class NotificationUnauthorizedTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();

    // ── Templates ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_NotificationTemplates_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/notifications/templates");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_NotificationTemplate_ById_NoToken_Returns401()
    {
        var response = await _client.GetAsync($"/notifications/templates/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_NotificationTemplate_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/notifications/templates", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PUT_NotificationTemplate_NoToken_Returns401()
    {
        var response = await _client.PutAsync($"/notifications/templates/{Guid.NewGuid()}", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_ActivateTemplate_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/notifications/templates/{Guid.NewGuid()}/activate", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Preferences ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_NotificationPreferences_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/notifications/preferences");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PUT_NotificationPreferences_NoToken_Returns401()
    {
        var response = await _client.PutAsync("/notifications/preferences", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Inbox ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GET_Inbox_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/notifications/inbox");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_InboxUnreadCount_NoToken_Returns401()
    {
        var response = await _client.GetAsync("/notifications/inbox/unread-count");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_MarkRead_NoToken_Returns401()
    {
        var response = await _client.PostAsync($"/notifications/inbox/{Guid.NewGuid()}/read", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task POST_MarkAllRead_NoToken_Returns401()
    {
        var response = await _client.PostAsync("/notifications/inbox/read-all", null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
