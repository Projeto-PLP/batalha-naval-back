using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Domain.Interfaces;
using BatalhaNaval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BatalhaNaval.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly BatalhaNavalDbContext _context;

    public UserRepository(BatalhaNavalDbContext context)
    {
        _context = context;
    }

    public async Task<User> AddAsync(User user)
    {
        await _context.Users.AddAsync(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User?> GetByUsernameAsync(string username)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Username == username);
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users
            .AsNoTracking()
            .Include(u => u.Profile)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username == username);
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        return await _context.Users.AnyAsync(u => u.Id == id);
    }

    public async Task<List<PlayerProfile>> GetTopPlayersAsync(int count)
    {
        return await _context.PlayerProfiles
            .Include(p => p.User) //para pegar o Username no DTO do ranking
            .OrderByDescending(p => p.RankPoints)
            .Take(count)
            .ToListAsync();
    }
}