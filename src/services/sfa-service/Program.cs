using CrmPlatform.SfaService.Api;
using CrmPlatform.SfaService.Application.Accounts;
using CrmPlatform.SfaService.Application.Contacts;
using CrmPlatform.SfaService.Application.Leads;
using CrmPlatform.SfaService.Application.Opportunities;
using CrmPlatform.SfaService.Infrastructure.Data;
using CrmPlatform.SfaService.Infrastructure.Messaging;
using CrmPlatform.ServiceTemplate.Infrastructure;
using CrmPlatform.ServiceTemplate.Infrastructure.Messaging;
using Hellang.Middleware.ProblemDetails;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Platform bootstrap (auth, health, OTel, SB client) ──────────────────────
builder.Services.AddCrmService(builder.Configuration, builder.Environment, "sfa-service");

// ─── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<SfaDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "sfa")));

// ─── Idempotency store ────────────────────────────────────────────────────────
builder.Services.AddScoped<IIdempotencyStore, SfaIdempotencyStore>();

// ─── Application handlers ─────────────────────────────────────────────────────
builder.Services.AddScoped<CreateLeadHandler>();
builder.Services.AddScoped<UpdateLeadHandler>();
builder.Services.AddScoped<AssignLeadHandler>();
builder.Services.AddScoped<ConvertLeadHandler>();
builder.Services.AddScoped<DeleteLeadHandler>();
builder.Services.AddScoped<ScoreDecayHandler>();
builder.Services.AddScoped<CreateOpportunityHandler>();
builder.Services.AddScoped<AdvanceStageHandler>();
builder.Services.AddScoped<CreateContactHandler>();
builder.Services.AddScoped<CreateAccountHandler>();

// ─── Background services ──────────────────────────────────────────────────────
builder.Services.AddHostedService<ScoreDecayService>();

// ─── Service Bus consumers (hosted services) ──────────────────────────────────
builder.Services.Configure<ServiceBusConsumerOptions>(
    builder.Configuration.GetSection("ServiceBus:ConsumerOptions"));

builder.Services.AddSingleton<TenantSuspendedConsumer>();
builder.Services.AddSingleton<JourneyCompletedConsumer>();

builder.Services.AddHostedService<ConsumerHostedService<TenantSuspendedConsumer>>();
builder.Services.AddHostedService<ConsumerHostedService<JourneyCompletedConsumer>>();

// ─── Problem Details (RFC 7807) ───────────────────────────────────────────────
builder.Services.AddProblemDetails(opts =>
{
    opts.IncludeExceptionDetails = (ctx, _) => ctx.RequestServices
        .GetRequiredService<IHostEnvironment>().IsDevelopment();
});

// ─── Memory cache ─────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ─── OpenAPI / Swagger ────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── Database migration ───────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SfaDbContext>();
    await db.Database.MigrateAsync();
}

// ─── Middleware pipeline ──────────────────────────────────────────────────────
app.UseProblemDetails();
app.UseCrmService(); // health checks + auth + TenantContextMiddleware

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ─── Endpoints ────────────────────────────────────────────────────────────────
app.MapSfaEndpoints();
app.MapSfaInternalEndpoints();

app.Run();

// ─── Consumer hosted service wrapper ─────────────────────────────────────────
/// <summary>
/// Generic IHostedService wrapper that starts/stops a BaseServiceBusConsumer.
/// </summary>
public sealed class ConsumerHostedService<TConsumer>(TConsumer consumer) : IHostedService
    where TConsumer : class
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        var method = typeof(TConsumer).GetMethod("StartAsync")!;
        return (Task)method.Invoke(consumer, [cancellationToken])!;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        var method = typeof(TConsumer).GetMethod("StopAsync")!;
        return (Task)method.Invoke(consumer, [cancellationToken])!;
    }
}

// Expose for integration tests
public partial class Program { }
