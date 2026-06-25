using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using OrderManagement.API.Middleware;
using OrderManagement.Application;
using OrderManagement.Application.Helpers;
using OrderManagement.Domain.Entities;
using OrderManagement.Domain.Enums;
using OrderManagement.Domain.Interfaces.Repositories;
using OrderManagement.Infrastructure;
using OrderManagement.Infrastructure.Data;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ──────────────────────────────────────────────────────────────
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<AppDbContext>("database");

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Order Management API",
        Version = "v1",
        Description = "API REST para gerenciamento de clientes, produtos, estoque e pedidos."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Informe o token JWT obtido em /api/auth/login.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
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

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
        c.IncludeXmlComments(xmlPath);
});

builder.Services.AddRateLimiter(options =>
{
    var authLimit = builder.Configuration.GetValue<int>("RateLimiting:Auth:PermitLimit", 10);
    var authWindow = builder.Configuration.GetValue<int>("RateLimiting:Auth:WindowSeconds", 60);
    var createLimit = builder.Configuration.GetValue<int>("RateLimiting:Create:PermitLimit", 30);
    var createWindow = builder.Configuration.GetValue<int>("RateLimiting:Create:WindowSeconds", 60);

    options.AddPolicy("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(authWindow),
                PermitLimit = authLimit,
                QueueLimit = 0
            }));

    options.AddPolicy("create", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? httpContext.Connection.RemoteIpAddress?.ToString()
            ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(createWindow),
                PermitLimit = createLimit,
                QueueLimit = 0
            }));

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            code = "RATE_LIMIT_EXCEEDED",
            message = "Muitas requisições. Aguarde antes de tentar novamente."
        }, token);
    };
});

// ─── Pipeline ─────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Order Management API v1"));
}

app.UseHttpsRedirection();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            duration = report.TotalDuration,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                duration = e.Value.Duration,
                description = e.Value.Description,
                error = e.Value.Exception?.Message
            })
        });
        await context.Response.WriteAsync(result);
    }
});

// Auto-apply migrations and seed on startup (development convenience)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    // Seed admin user if no users exist
    var userRepo = scope.ServiceProvider.GetRequiredService<IUserRepository>();
    if (!await userRepo.AnyAsync())
    {
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var adminName = config["AdminSeed:Name"] ?? "Administrador";
        var adminEmail = config["AdminSeed:Email"] ?? "admin@orderapi.com";
        var adminPassword = config["AdminSeed:Password"] ?? "Admin@123";

        var admin = new User(adminName, adminEmail, PasswordHelper.Hash(adminPassword), UserRole.Admin);
        await userRepo.AddAsync(admin);
        var uow = scope.ServiceProvider.GetRequiredService<OrderManagement.Domain.Interfaces.IUnitOfWork>();
        await uow.CommitAsync();
    }
}

app.Run();
