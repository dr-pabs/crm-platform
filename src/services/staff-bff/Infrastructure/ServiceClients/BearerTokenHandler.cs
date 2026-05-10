using Microsoft.AspNetCore.Http;

namespace CrmPlatform.StaffBff.Infrastructure.ServiceClients;

/// <summary>
/// Forwards the inbound Bearer token to downstream service calls so tenant
/// context and identity are preserved without re-issuing tokens.
/// </summary>
public sealed class BearerTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = accessor.HttpContext?.Request.Headers.Authorization
            .ToString()
            .Replace("Bearer ", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(token))
            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return base.SendAsync(request, cancellationToken);
    }
}
