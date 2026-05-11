using CrmPlatform.AiOrchestrationService.Domain.Enums;
using CrmPlatform.AiOrchestrationService.Infrastructure.Claude;

namespace CrmPlatform.TestStubs;

/// <summary>
/// Fake AI model client that returns pre-configured responses for testing.
/// No Azure dependency. Useful for E2E tests that need AI responses without
/// calling real Claude/GPT endpoints.
/// </summary>
public sealed class StubModelClient : IClaudeClient
{
    private readonly Dictionary<(CapabilityType, UseCase), string> _responses = new();

    /// <summary>Register a canned response for a capability+useCase combination.</summary>
    public StubModelClient WithResponse(CapabilityType capability, UseCase useCase, string content)
    {
        _responses[(capability, useCase)] = content;
        return this;
    }

    public Task<ClaudeResponse> CompleteAsync(
        Guid tenantId,
        CapabilityType capabilityType,
        UseCase useCase,
        object templateVars,
        CancellationToken ct = default)
    {
        var content = _responses.TryGetValue((capabilityType, useCase), out var r)
            ? r
            : GetDefaultResponse(capabilityType);

        return Task.FromResult(new ClaudeResponse(
            Content:      content,
            ModelName:    "stub-model",
            PromptUsed:   "stub",
            InputTokens:  10,
            OutputTokens: 20));
    }

    private static string GetDefaultResponse(CapabilityType capability) => capability switch
    {
        CapabilityType.LeadScoring =>
            """{"score": 75, "rationale": "Good fit based on company size and industry.", "confidence": 0.85}""",

        CapabilityType.EmailDraft =>
            "Dear {{leadName}},\n\nThank you for your interest in our CRM platform. I would love to schedule a demo.\n\nBest regards,\nSales Team",

        CapabilityType.CaseSummarisation =>
            "Customer reported login issues. Issue resolved by resetting MFA. No further action required.",

        CapabilityType.SentimentAnalysis =>
            """{"sentiment": "Neutral", "score": 0.65}""",

        CapabilityType.NextBestAction =>
            """{"action": "Schedule follow-up call within 48 hours", "rationale": "Lead showed high engagement with email campaign"}""",

        CapabilityType.JourneyPersonalisation =>
            """{"recommendedBranchId": "00000000-0000-0000-0000-000000000001", "rationale": "Customer matches high-engagement segment"}""",

        CapabilityType.SmsComposition =>
            "Hi {{contactName}}, check out our latest CRM features at https://example.com. Reply STOP to opt out.",

        CapabilityType.TeamsNotification =>
            "New lead assigned: {{leadName}} from {{company}}. Priority: High.",

        CapabilityType.KnowledgeQuery =>
            "Based on the documentation, the recommended approach is to verify the integration settings and check API credentials.",

        CapabilityType.PipelineForecasting =>
            """{"forecast": 1250000, "lowEstimate": 950000, "highEstimate": 1600000, "confidence": 0.78, "rationale": "Pipeline weighted by stage probability"}""",

        CapabilityType.ChurnPrediction =>
            """{"churnRisk": "Medium", "riskScore": 45, "signals": ["Decreased login frequency", "3 open cases"], "recommendedAction": "Schedule account review call"}""",

        _ => """{"result": "ok", "confidence": 0.9}"""
    };
}

/// <summary>
/// Factory methods for creating common stub configurations.
/// </summary>
public static class StubModelFactory
{
    /// <summary>Default stub with responses for all 12 capabilities.</summary>
    public static StubModelClient CreateDefault() => new StubModelClient();

    /// <summary>Stub that simulates high lead scores (all leads scored 90+).</summary>
    public static StubModelClient CreateHighScoring()
    {
        var client = new StubModelClient();
        client.WithResponse(CapabilityType.LeadScoring, UseCase.LeadCreated,
            """{"score": 95, "rationale": "Enterprise company with immediate need.", "confidence": 0.95}""");
        client.WithResponse(CapabilityType.LeadScoring, UseCase.LeadAssigned,
            """{"score": 92, "rationale": "Re-evaluated after assignment.", "confidence": 0.90}""");
        return client;
    }

    /// <summary>Stub that simulates negative sentiment (all comments negative).</summary>
    public static StubModelClient CreateNegativeSentiment()
    {
        var client = new StubModelClient();
        client.WithResponse(CapabilityType.SentimentAnalysis, UseCase.CaseCommentAdded,
            """{"sentiment": "Negative", "score": 0.12}""");
        return client;
    }

    /// <summary>Stub that simulates model failure (throws on every call).</summary>
    public static IClaudeClient CreateFailing() => new FailingModelClient();
}

/// <summary>
/// Model client that always fails — for testing error handling and fallback chains.
/// </summary>
public sealed class FailingModelClient : IClaudeClient
{
    public Task<ClaudeResponse> CompleteAsync(
        Guid tenantId, CapabilityType capabilityType, UseCase useCase,
        object templateVars, CancellationToken ct = default)
        => throw new InvalidOperationException("Simulated model failure");
}
