using System.Text.Json;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Interfaces;
using Microsoft.Extensions.Caching.Distributed;

namespace BatalhaNaval.Infrastructure.Repositories;

public class MatchStateRepository : IMatchStateRepository
{
    // Prefixo para organizar as chaves no Redis (ex: "match:GUID")
    private const string KeyPrefix = "match";
    private readonly IDistributedCache _cache;
    private readonly JsonSerializerOptions _jsonOptions;

    public MatchStateRepository(IDistributedCache cache)
    {
        _cache = cache;

        // Configurações de Serialização para garantir que o DTO seja salvo corretamente
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false // False para economizar bytes no Redis
        };
    }

    public async Task SaveStateAsync(Match match)
    {
        // 1. Converte Domain -> DTO
        var dto = match.ToRedisDto();

        // 2. Serializa para JSON
        var json = JsonSerializer.Serialize(dto, _jsonOptions);

        // 3. Define a chave e o tempo de vida (TTL)
        var key = $"{KeyPrefix}:{match.Id}";

        var options = new DistributedCacheEntryOptions
        {
            // O jogo expira se ficar 1 hora sem interação (Slide)
            SlidingExpiration = TimeSpan.FromHours(1)
        };

        // 4. Salva no Redis
        await _cache.SetStringAsync(key, json, options);
    }

    public async Task<Match?> GetStateAsync(Guid matchId)
    {
        var key = $"{KeyPrefix}:{matchId}";
        var json = await _cache.GetStringAsync(key);

        if (string.IsNullOrEmpty(json))
            return null;

        try
        {
            // 1. Deserializa JSON -> DTO
            var dto = JsonSerializer.Deserialize<MatchRedis>(json, _jsonOptions);

            if (dto == null) return null;

            // 2. Converte DTO -> Domain (Reconstrução / Hidratação)
            return Match.FromRedisDto(dto);
        }
        catch (JsonException)
        {
            // Se o JSON estiver corrompido, tratamos como não encontrado
            return null;
        }
    }

    public async Task DeleteStateAsync(Guid matchId)
    {
        var key = $"{KeyPrefix}:{matchId}";
        await _cache.RemoveAsync(key);
    }

    public async Task<bool> ExistsAsync(Guid matchId)
    {
        var key = $"{KeyPrefix}:{matchId}";
        // O IDistributedCache não tem um método "Exists" nativo otimizado, mas ler o header é rápido o suficiente.
        var bytes = await _cache.GetAsync(key);
        return bytes != null && bytes.Length > 0;
    }
}