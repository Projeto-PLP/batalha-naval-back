using BatalhaNaval.API.BackgroundServices;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BatalhaNaval.API.Middlewares;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Application.Services;
using BatalhaNaval.Application.Validators;
using BatalhaNaval.Domain.Interfaces;
using BatalhaNaval.Infrastructure.Persistence;
using BatalhaNaval.Infrastructure.Repositories;
using BatalhaNaval.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"]!);
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("front-cors", policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
builder.Services.AddAuthentication(x =>
    {
        x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(x =>
    {
        // TODO dev: false | production: true
        x.RequireHttpsMetadata = false;
        x.SaveToken = true;
        x.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtSettings["Audience"]
        };
    });

builder.Services.AddScoped<IPasswordService, PasswordService>();
builder.Services.AddScoped<ITokenService, TokenService>();

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "BatalhaNaval_";
});

builder.Services.AddScoped<ICacheService, RedisCacheService>();

// ==================================================================
// 1. Configuração de Banco de Dados (PostgreSQL)
// ==================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Garante que a string existe antes de subir
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "A ConnectionString 'DefaultConnection' não foi encontrada no appsettings.json.");

// TODO avaliar unificação da configuração do DbContext para evitar conflitos (comentado abaixo)
builder.Services.AddDbContext<BatalhaNavalDbContext>(options =>
    options.UseNpgsql(connectionString));

// builder.Services.AddDbContext<BatalhaNavalDbContext>(options =>
// {
//     options.UseNpgsql(connectionString, npgsqlOptions =>
//     {
//         npgsqlOptions.EnableRetryOnFailure(
//             5,
//             TimeSpan.FromSeconds(10),
//             null);
//     });
//
//     if (builder.Environment.IsDevelopment())
//     {
//         options.EnableSensitiveDataLogging();
//         options.EnableDetailedErrors();
//     }
// });

// ==================================================================
// 2. Injeção de Dependência (DI)
// ==================================================================
// Infraestrutura (Quem implementa o acesso a dados)
builder.Services.AddScoped<IMatchRepository, MatchRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IMedalRepository, MedalRepository>();
builder.Services.AddScoped<IMatchStateRepository, MatchStateRepository>();

// Aplicação (Quem detém a lógica de orquestração e IA)
builder.Services.AddScoped<IMatchService, MatchService>();

// Background Service: verifica timeout de turno em partidas contra IA a cada 5s
builder.Services.AddHostedService<AiTimeoutBackgroundService>();

// ==================================================================
// 3. Configuração da API e Serialização JSON
// ==================================================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Converte Enums para String na API (Ex: "Dynamic", "Water", "Hit")
        // Isso facilita a leitura pelo Frontend/BFF
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());

        // Ignora campos nulos no JSON de resposta para economizar banda
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// FluentValidation
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<PlaceShipsValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<ShootInputValidator>();

// ==================================================================
// 4. Configuração da Documentação (OpenAPI / Scalar)
// ==================================================================
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info.Title = "Batalha Naval PLP - Core API";
        document.Info.Version = "v1.0";
        document.Info.Description = "API responsável pelas regras de jogo, persistência e Inteligência Artificial.";
        return Task.CompletedTask;
    });
});

builder.Services.AddDbContext<BatalhaNavalDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            5,
            TimeSpan.FromSeconds(10),
            null);
    });

    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.EnableDetailedErrors();
    }
});

builder.Services.AddScoped<IUserRepository, UserRepository>();

builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), new[] { "liveness" })
    .AddNpgSql(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "readiness", "db" })
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")!,
        "redis",
        HealthStatus.Unhealthy,
        new[] { "readiness", "cache" });

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();

// ==================================================================
// 5. Pipeline de Execução (Middleware)
// ==================================================================
if (app.Environment.IsDevelopment())
{
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/openapi/v1.json")
        {
            var originalBody = context.Response.Body;
            using var newBody = new MemoryStream();
            context.Response.Body = newBody;

            await next();

            context.Response.Body = originalBody;
            newBody.Seek(0, SeekOrigin.Begin);
            var json = await new StreamReader(newBody).ReadToEndAsync();

            var root = JsonNode.Parse(json);
            if (root is JsonObject obj)
            {
                var components = obj["components"] as JsonObject ?? new JsonObject();
                obj["components"] = components;

                var schemes = components["securitySchemes"] as JsonObject ?? new JsonObject();
                components["securitySchemes"] = schemes;

                if (!schemes.ContainsKey("Bearer"))
                    schemes["Bearer"] = JsonNode.Parse("""
                                                       {
                                                           "type": "http",
                                                           "scheme": "bearer",
                                                           "bearerFormat": "JWT",
                                                           "description": "Insira o token JWT aqui."
                                                       }
                                                       """);

                var security = obj["security"] as JsonArray ?? new JsonArray();
                obj["security"] = security;

                var hasBearer = security.Any(x => x is JsonObject s && s.ContainsKey("Bearer"));
                if (!hasBearer) security.Add(JsonNode.Parse("""{ "Bearer": [] }"""));

                var modifiedJson = obj.ToString();
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(modifiedJson);
                return;
            }
        }

        await next();
    });

    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Batalha Naval Docs")
            .WithTheme(ScalarTheme.Mars)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient)
            .WithPreferredScheme("Bearer");
    });
}

app.UseMiddleware<JwtBlocklistMiddleware>();

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

app.UseCors("front-cors");

app.MapControllers();

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("liveness")
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = r => r.Tags.Contains("readiness"),
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Dica: Logs iniciais para debug
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Iniciando Batalha Naval API - .NET 10");

app.Run();

public partial class Program
{
}