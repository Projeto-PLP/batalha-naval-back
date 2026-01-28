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

    public async Task<bool> ExistsByUsernameAsync(string username)
    {
        return await _context.Users.AnyAsync(u => u.Username == username);
    }
}