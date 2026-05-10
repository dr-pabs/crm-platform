using CrmPlatform.ServiceTemplate.Infrastructure;
using CrmPlatform.StaffBff.Api;
using CrmPlatform.StaffBff.Application;
using CrmPlatform.StaffBff.Infrastructure.ServiceClients;
using Hellang.Middleware.ProblemDetails;
using Microsoft.Extensions.Http.Resilience;

var builder = WebApplication.CreateBuilder(args);

// ─── Platform bootstrap (auth, health, OTel) ─────────────────────────────────
// No Service Bus client — BFF only makes outbound HTTP, no domain events.
builder.Services.AddCrmService(builder.Configuration, builder.Environment, "staff-bff");

// ─── Token forwarding ─────────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<BearerTokenHandler>();

// ─── Downstream HTTP clients with resilience pipelines ───────────────────────
static IHttpClientBuilder AddServiceClient<TClient>(
    IServiceCollection services, IConfiguration config, string name)
    where TClient : class
{
    return services
        .AddHttpClient<TClient>(c =>
        {
            var address = config[$"ServiceClients:{name}:BaseAddress"]
                ?? throw new InvalidOperationException(
                    $"ServiceClients:{name}:BaseAddress is not configured");
            c.BaseAddress = new Uri(address);
        })
        .AddHttpMessageHandler<BearerTokenHandler>()
        .AddStandardResilienceHandler();
}

AddServiceClient<SfaServiceClient>(
    builder.Services, builder.Configuration, "SfaService");

AddServiceClient<CssServiceClient>(
    builder.Services, builder.Configuration, "CssService");

AddServiceClient<MarketingServiceClient>(
    builder.Services, builder.Configuration, "MarketingService");

AddServiceClient<AnalyticsServiceClient>(
    builder.Services, builder.Configuration, "AnalyticsService");

// ─── Application layer ────────────────────────────────────────────────────────
builder.Services.AddScoped<DashboardAggregator>();

// ─── Problem Details (RFC 7807) ───────────────────────────────────────────────
builder.Services.AddProblemDetails(opts =>
{
    opts.IncludeExceptionDetails = (ctx, _) => ctx.RequestServices
        .GetRequiredService<IHostEnvironment>().IsDevelopment();
});

// ─── OpenAPI / Swagger ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── Middleware pipeline ──────────────────────────────────────────────────────
app.UseProblemDetails();
app.UseCrmService(); // health checks + auth + TenantContextMiddleware

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ─── Endpoints ────────────────────────────────────────────────────────────────
app.MapBffEndpoints();

app.Run();

public partial class Program { }
