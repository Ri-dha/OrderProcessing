using JasperFx.Resources;
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Application;
using OrderProcessing.Application.Features;
using OrderProcessing.Application.Services;
using OrderProcessing.Infrastructure.Persistence;
using OrderProcessing.Middleware;
using Serilog;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.ErrorHandling;
using Wolverine.Http;
using Wolverine.Postgresql;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(
        outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseResourceSetupOnStartup();
builder.Host.UseSerilog();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? throw new InvalidOperationException("DefaultConnection is required.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddWolverineHttp();

builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(AssemblyMarker).Assembly);
    opts.PersistMessagesWithPostgresql(connectionString);
    opts.Policies.UseDurableLocalQueues();
    opts.UseEntityFrameworkCoreTransactions();
    opts.Policies.AutoApplyTransactions();
    opts.Policies.OnException<DbUpdateConcurrencyException>().RetryTimes(5);
});

builder.Services.AddHostedService<IdempotencyCleanupService>();
builder.Services.AddHostedService<OrderExpiryService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging();

app.MapWolverineEndpoints(opts =>
{
    opts.UseDataAnnotationsValidationProblemDetailMiddleware();
});
app.Run();

public partial class Program;
