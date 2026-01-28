using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using System.Text.Json.Serialization;
using BatalhaNaval.Infrastructure.Persistence;
using BatalhaNaval.Infrastructure.Repositories;
using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Application.Services;
using BatalhaNaval.Domain.Interfaces;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

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
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Batalha Naval Docs")
            .WithTheme(ScalarTheme.Mars)
            .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
    });
}

app.UseHttpsRedirection();

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