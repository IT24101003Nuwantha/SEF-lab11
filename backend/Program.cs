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

// Helper to format PostgreSQL / Supabase connection strings (handles both URI and ADO.NET key-value formats)
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
            var userInfo = uri.UserInfo.Split(':');
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var database = uri.AbsolutePath.TrimStart('/');
            if (string.IsNullOrEmpty(database)) database = "postgres";

            var npgsqlBuilder = new Npgsql.NpgsqlConnectionStringBuilder
            {
                Host = host,
                Port = port,
                Database = database,
                Username = username,
                Password = password,
                SslMode = Npgsql.SslMode.Require
            };
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

// Add DbContext (supports DATABASE_URL, SUPABASE_DB_URL, ConnectionStrings__Default, or appsettings.json)
var rawConnectionString = 
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("SUPABASE_DB_URL")
    ?? Environment.GetEnvironmentVariable("ConnectionStrings__Default")
    ?? builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["ConnectionStrings:Default"]
    ?? builder.Configuration["ConnectionStrings__Default"];

var connectionString = FormatConnectionString(rawConnectionString);
builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(connectionString));

// Add services to the container.
builder.Services.AddScoped<IMenuService, MenuService>();
builder.Services.AddControllers();

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

// Swagger Bearer Token Setup
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
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

// Ensure DB is migrated and seeded
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        db.Database.Migrate();
        if (!db.MenuItems.Any())
        {
            db.MenuItems.AddRange(
                new MenuItem { Name = "Kottu Roti", Price = 750m, Category = "Mains" },
                new MenuItem { Name = "Fried Rice", Price = 850m, Category = "Mains" },
                new MenuItem { Name = "Watalappan", Price = 350m, Category = "Dessert" });
            db.SaveChanges();
        }
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating or seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Frontend");

app.UseAuthentication(); // AuthN before AuthZ
app.UseAuthorization();  // AuthZ

app.MapControllers();

app.Run();