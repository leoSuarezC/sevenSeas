using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.WebAPI.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

string connectionString = builder.Configuration.GetConnectionString("SpecimenCheckIn")
    ?? throw new InvalidOperationException(
        "No 'SpecimenCheckIn' connection string configured. Copy .env.example to .env, or set ConnectionStrings__SpecimenCheckIn.");

// One TenantContext per request, shared by the middleware that resolves it, the
// interceptor that publishes it to SESSION_CONTEXT, and the DbContext that filters on it.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(services => services.GetRequiredService<TenantContext>());
builder.Services.AddScoped<TenantSessionInterceptor>();

builder.Services.AddDbContext<SpecimenCheckInContext>((services, options) =>
    options
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
            "__EFMigrationsHistory",
            SpecimenCheckInContext.Schema))
        .AddInterceptors(services.GetRequiredService<TenantSessionInterceptor>()));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Ahead of the endpoints: no handler ever runs without a resolved lab.
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

app.Run();

/// <summary>
/// Exposed so the integration tests can host the API through WebApplicationFactory.
/// </summary>
public partial class Program;
