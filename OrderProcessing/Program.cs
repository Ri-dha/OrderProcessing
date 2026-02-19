using Microsoft.EntityFrameworkCore;
using OrderProcessing.Domain.repositories;
using OrderProcessing.Infrastructure.Persistence;
using OrderProcessing.Infrastructure.repositories;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Http;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Database Connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// 2. Register Repositories
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();

// 3. Configure Wolverine
builder.Host.UseWolverine(opts =>
{
    // Tells Wolverine to find our Command Handlers in the Application project
    opts.Discovery.IncludeAssembly(typeof(OrderProcessing.Application.AssemblyMarker).Assembly);
    
    // Automatically wrap handlers in a database transaction and call SaveChanges!
    opts.UseEntityFrameworkCoreTransactions();
});

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// 4. Map Wolverine HTTP endpoints
app.MapWolverineEndpoints();

app.Run();