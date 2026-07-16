using Microsoft.EntityFrameworkCore;
using SpecimenCheckIn.Commands.Manifests;
using SpecimenCheckIn.Context;
using SpecimenCheckIn.Context.Tenancy;
using SpecimenCheckIn.Queries.Manifests;
using SpecimenCheckIn.Queries.Session;
using SpecimenCheckIn.WebAPI.Middleware;
using SpecimenCheckIn.WebAPI.Seeding;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Rebuilds the database from the migrations and reseeds it, discarding whatever was there.
// For getting back to a known state after clicking around — see the README.
bool resetDatabase = args.Contains("--reset-db", StringComparer.OrdinalIgnoreCase);

string connectionString = builder.Configuration.GetConnectionString("SpecimenCheckIn")
    ?? throw new InvalidOperationException(
        "No 'SpecimenCheckIn' connection string configured. Copy .env.example to .env, or set ConnectionStrings__SpecimenCheckIn.");

// One TenantContext per request, shared by the middleware that resolves it, the
// interceptor that publishes it to SESSION_CONTEXT, and the DbContext that filters on it.
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(services => services.GetRequiredService<TenantContext>());
builder.Services.AddScoped<UserContext>();
builder.Services.AddScoped<IUserContext>(services => services.GetRequiredService<UserContext>());
builder.Services.AddScoped<TenantSessionInterceptor>();

builder.Services.AddDbContext<SpecimenCheckInContext>((services, options) =>
    options
        .UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(
            "__EFMigrationsHistory",
            SpecimenCheckInContext.Schema))
        .AddInterceptors(services.GetRequiredService<TenantSessionInterceptor>()));

builder.Services.AddScoped<ManifestQueries>();
builder.Services.AddScoped<SessionQueries>();
builder.Services.AddScoped<CheckInCommands>();

// Injected rather than calling DateTime.UtcNow, so time is one more thing a test can pin
// down instead of race against.
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<CheckInExceptionHandler>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// The Vue dev server is a different origin; the API is local-only, so this stays narrow.
const string devCors = "dev";
builder.Services.AddCors(options => options.AddPolicy(devCors, policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()));

WebApplication app = builder.Build();

// A flag that erases everything should not be one typo away from running against real
// data, so it is refused rather than ignored anywhere but a developer's machine.
if (resetDatabase && !app.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        $"--reset-db drops the database and is only available in Development (this is {app.Environment.EnvironmentName}).");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(devCors);

    // Migrate and seed on start, so "clone, run" is the whole setup. Existing data is left
    // alone: a technician's work should survive a restart, and the seed only fills a gap.
    await DatabaseSeeder.SeedAsync(app.Services, resetDatabase);
}

app.UseExceptionHandler();
app.UseStatusCodePages();

// Ahead of the endpoints: no handler ever runs without a resolved lab.
app.UseMiddleware<TenantMiddleware>();

app.MapControllers();

await app.RunAsync();

/// <summary>
/// Exposed so the integration tests can host the API through WebApplicationFactory.
/// </summary>
public partial class Program;
