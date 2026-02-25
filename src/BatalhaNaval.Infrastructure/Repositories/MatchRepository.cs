using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Enums;
using BatalhaNaval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BatalhaNaval.Infrastructure.Repositories;

public class MatchRepository : IMatchRepository
{
    private readonly BatalhaNavalDbContext _context;

    public MatchRepository(BatalhaNavalDbContext context)
    {
        _context = context;
    }

    public async Task<Match?> GetByIdAsync(Guid id)
    {
        // O EF Core já vai fazer a conversão do JSONB para o objeto Board automaticamente
        return await _context.Matches.FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task SaveAsync(Match match)
    {
        // 1. Verifica se a entidade já está sendo rastreada na memória pelo EF
        var entry = _context.ChangeTracker.Entries<Match>()
            .FirstOrDefault(e => e.Entity.Id == match.Id);

        if (entry != null)
        {
            // CENÁRIO A: A entidade já está na memória (Tracked).
            // Isso acontece quando carregamos via GetByIdAsync e modificamos.
            // AÇÃO: Forçamos a marcação de 'Modified' nas colunas críticas (JSON)

            entry.State = EntityState.Modified; // Marca tudo como modificado por segurança

            // Força explicitamente as propriedades JSON
            entry.Property(p => p.Player1Board).IsModified = true;
            entry.Property(p => p.Player2Board).IsModified = true;

            // Força propriedades de estado que mudam frequentemente
            entry.Property(p => p.Status).IsModified = true;
            entry.Property(p => p.CurrentTurnPlayerId).IsModified = true;
            entry.Property(p => p.LastMoveAt).IsModified = true;
            entry.Property(p => p.HasMovedThisTurn).IsModified = true;
            entry.Property(p => p.Player1Hits).IsModified = true;
            entry.Property(p => p.Player2Hits).IsModified = true;
            entry.Property(p => p.Player1Misses).IsModified = true;
            entry.Property(p => p.Player2Misses).IsModified = true;
            entry.Property(p => p.Player1ConsecutiveHits).IsModified = true;
            entry.Property(p => p.Player2ConsecutiveHits).IsModified = true;
        }
        else
        {
            // CENÁRIO B: A entidade NÃO está na memória (Detached).
            // Isso acontece em 'StartMatch' (Objeto Novo) ou quando vem do Redis (Objeto Reconstruído).
            // AÇÃO: Verificar no banco se é INSERT ou UPDATE.

            var exists = await _context.Matches.AnyAsync(m => m.Id == match.Id);

            if (exists)
                // Se JÁ EXISTE no banco -> UPDATE
                _context.Matches.Update(match);
            else
                // Se NÃO EXISTE no banco -> INSERT
                await _context.Matches.AddAsync(match);
        }

        await _context.SaveChangesAsync();
    }

    public async Task<PlayerProfile> GetUserProfileAsync(Guid userId)
    {
        var profile = await _context.PlayerProfiles.FindAsync(userId);

        if (profile == null)
        {
            profile = new PlayerProfile { UserId = userId };
            await _context.PlayerProfiles.AddAsync(profile);
            await _context.SaveChangesAsync();
        }

        return profile;
    }

    public async Task UpdateUserProfileAsync(PlayerProfile profile)
    {
        _context.PlayerProfiles.Update(profile);
        await _context.SaveChangesAsync();
    }

    public async Task<Guid?> GetActiveMatchIdAsync(Guid userId)
    {
        var matchId = await _context.Matches
            .Where(m => (m.Player1Id == userId || m.Player2Id == userId)
                        && m.Status != MatchStatus.Finished)
            .Select(m => m.Id)
            .FirstOrDefaultAsync();
        return matchId == Guid.Empty ? null : matchId;
    }

    public async Task<List<Guid>> GetActiveAiMatchIdsAsync()
    {
        return await _context.Matches
            .Where(m => m.Player2Id == null && m.Status == MatchStatus.InProgress)
            .Select(m => m.Id)
            .ToListAsync();
    }

    public async Task UpdateAsync(Match match)
    {
        _context.Matches.Update(match);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(Match match)
    {
        _context.Matches.Remove(match);
        await _context.SaveChangesAsync();
    }
}