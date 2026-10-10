using CampusEats.Api.Data;
using CampusEats.Api.Models;
using CampusEats.Api.Services;
using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using dotenv.net;

// Load environment variables from .env file (supports backend/.env or root .env)
DotEnv.Load(options: new DotEnvOptions(
    ignoreExceptions: true,
    envFilePaths: new[] { ".env", "../.env" }
));

var builder = WebApplication.CreateBuilder(args);

// Railway Port Configuration (Task 2)
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Forwarded Headers for Railway Reverse Proxy (Task 6)
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
                               Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Helper to format PostgreSQL / Supabase connection strings (handles both URI and ADO.NET formats)
static string FormatConnectionString(string? rawConnStr)
{
    if (string.IsNullOrWhiteSpace(rawConnStr)) return rawConnStr ?? "";

    // Handle postgresql:// or postgres:// URI format (commonly copied from Supabase / Neon / Railway)
    if (rawConnStr.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        rawConnStr.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(rawConnStr);
            var userInfo = uri.UserInfo;
            var colonIdx = userInfo.IndexOf(':');
            var username = colonIdx >= 0 ? Uri.UnescapeDataString(userInfo.Substring(0, colonIdx)) : Uri.UnescapeDataString(userInfo);
            var password = colonIdx >= 0 ? Uri.UnescapeDataString(userInfo.Substring(colonIdx + 1)) : "";
            var host = uri.Host;
            var portNum = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrEmpty(database)) database = "postgres";

            var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = portNum,
                Database = database,
                Username = username,
                Password = password,
                SslMode = Npgsql.SslMode.Require
            };

            // If connecting through Supabase PgBouncer pooler (port 6543), disable client-side pooling issues
            if (portNum == 6543)
            {
                npgsqlBuilder.Pooling = false;
            }

            return npgsqlBuilder.ConnectionString;
        }
        catch
        {
            return rawConnStr;
        }
    }

    // Handle key-value format for Supabase / remote databases to ensure SSL is required
    if (rawConnStr.Contains("supabase.co", StringComparison.OrdinalIgnoreCase) ||
        rawConnStr.Contains("supabase.com", StringComparison.OrdinalIgnoreCase) ||
        rawConnStr.Contains("pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder(rawConnStr);
            if (npgsqlBuilder.SslMode == Npgsql.SslMode.Disable || npgsqlBuilder.SslMode == Npgsql.SslMode.Prefer)
            {
                npgsqlBuilder.SslMode = Npgsql.SslMode.Require;
            }
            return npgsqlBuilder.ConnectionString;
        }
        catch
        {
            return rawConnStr;
        }
    }

    return rawConnStr;
}

// Add DbContext (supports ConnectionStrings__Default, DATABASE_URL, SUPABASE_DB_URL, or appsettings.json)
var rawConnectionString = 
    Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("SUPABASE_DB_URL")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["ConnectionStrings:Default"]
    ?? builder.Configuration["ConnectionStrings__Default"];

var connectionString = FormatConnectionString(rawConnectionString);
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

// Add services to the container.
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// Configure CORS
var frontendUrl = builder.Configuration["Frontend:Url"]
    ?? builder.Configuration["Frontend__Url"]
    ?? builder.Configuration["FRONTEND_URL"]
    ?? "http://localhost:5173";

var origins = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "http://localhost:5173" };
if (!string.IsNullOrWhiteSpace(frontendUrl))
{
    origins.Add(frontendUrl.TrimEnd('/'));
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(origins.ToArray())
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// JWT Authentication Setup
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? builder.Configuration["Jwt__Key"]
    ?? "dev-only-secret-change-me-min-32-characters!!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? builder.Configuration["Jwt__Issuer"]
    ?? "CampusEats";
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? builder.Configuration["Jwt__Audience"]
    ?? "CampusEatsUsers";

var key = Encoding.UTF8.GetBytes(jwtKey);
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt => opt.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateLifetime = true
    });
builder.Services.AddAuthorization();

// Swagger Bearer Token Setup (Task 5)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "CampusEats API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Paste ONLY the token - no 'Bearer ' prefix."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Forwarded headers for Railway / reverse-proxy
app.UseForwardedHeaders();

// Ensure DB is migrated and seeded on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Verifying and applying database migrations...");
        db.Database.Migrate();
        logger.LogInformation("Database migrations applied successfully.");

        if (!db.MenuItems.Any())
        {
            db.MenuItems.AddRange(
                new MenuItem { Name = "Kottu Roti", Price = 750m, Category = "Mains" },
                new MenuItem { Name = "Fried Rice", Price = 850m, Category = "Mains" },
                new MenuItem { Name = "Watalappan", Price = 350m, Category = "Dessert" });
            db.SaveChanges();
            logger.LogInformation("Initial menu items seeded.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while connecting to or migrating the database.");
    }
}

// Enable Swagger in Development and on Railway for lab testing (Task 5)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CampusEats API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("Frontend");

app.UseAuthentication(); // AuthN before AuthZ
app.UseAuthorization();  // AuthZ

// Simple Health & Info Endpoints (Task 4)
app.MapGet("/", () => Results.Ok(new { message = "CampusEats API is running" }));
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

// Database Diagnostic Health Check Endpoint
app.MapGet("/health/db", async (AppDbContext db) =>
{
    try
    {
        var canConnect = await db.Database.CanConnectAsync();
        var itemCount = canConnect ? await db.MenuItems.CountAsync() : 0;
        return Results.Ok(new
        {
            status = canConnect ? "connected" : "failed",
            provider = "PostgreSQL",
            menuItemCount = itemCount
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database connection failed",
            detail: ex.Message,
            statusCode: 500
        );
    }
});

app.MapControllers();

app.Run();