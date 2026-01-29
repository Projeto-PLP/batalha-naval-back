using System.Text;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using BatalhaNaval.Infrastructure.Persistence;
using BatalhaNaval.Infrastructure.Repositories;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Application.Services;
using BatalhaNaval.Domain.Interfaces;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using System.Text.Json.Nodes;

var builder = WebApplication.CreateBuilder(args);

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var key = Encoding.ASCII.GetBytes(jwtSettings["SecretKey"]!);

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

// ==================================================================
// 1. Configuração de Banco de Dados (PostgreSQL)
// ==================================================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Garante que a string existe antes de subir
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("A ConnectionString 'DefaultConnection' não foi encontrada no appsettings.json.");

builder.Services.AddDbContext<BatalhaNavalDbContext>(options =>
    options.UseNpgsql(connectionString));

// ==================================================================
// 2. Injeção de Dependência (DI)
// ==================================================================
// Infraestrutura (Quem implementa o acesso a dados)
builder.Services.AddScoped<IMatchRepository, MatchRepository>();

// Aplicação (Quem detém a lógica de orquestração e IA)
builder.Services.AddScoped<IMatchService, MatchService>();

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
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorCodesToAdd: null);
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
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: new[] { "liveness" })
    .AddNpgSql(
        connectionString: builder.Configuration.GetConnectionString("DefaultConnection"),
        name: "postgresql",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "readiness", "db" });

var app = builder.Build();

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
                {
                    schemes["Bearer"] = JsonNode.Parse("""
                    {
                        "type": "http",
                        "scheme": "bearer",
                        "bearerFormat": "JWT",
                        "description": "Insira o token JWT aqui."
                    }
                    """);
                }

                var security = obj["security"] as JsonArray ?? new JsonArray();
                obj["security"] = security;
                
                bool hasBearer = security.Any(x => x is JsonObject s && s.ContainsKey("Bearer"));
                if (!hasBearer)
                {
                    security.Add(JsonNode.Parse("""{ "Bearer": [] }"""));
                }
                
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

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

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