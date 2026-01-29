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
        // Verifica se já existe para decidir entre Add ou Update
        var exists = await _context.Matches.AnyAsync(m => m.Id == match.Id);

        if (exists)
            _context.Matches.Update(match);
        else
            await _context.Matches.AddAsync(match);

        await _context.SaveChangesAsync();
    }

    public async Task<PlayerProfile> GetUserProfileAsync(Guid userId)
    {
        var profile = await _context.PlayerProfiles.FindAsync(userId);

        // Se o perfil não existir, cria um novo em memória (será salvo depois)
        if (profile == null)
        {
            profile = new PlayerProfile { UserId = userId };
            await _context.PlayerProfiles.AddAsync(profile);
            // Salva logo para garantir que existe na próxima busca
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
}