using BatalhaNaval.Application.Interfaces;
using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BatalhaNaval.Infrastructure.Repositories;

public class CampaignRepository : ICampaignRepository
{
    private readonly BatalhaNavalDbContext _context;

    public CampaignRepository(BatalhaNavalDbContext context)
    {
        _context = context;
    }

    public async Task<CampaignProgress> GetOrCreateProgressAsync(Guid userId)
    {
        var progress = await _context.CampaignProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId);

        if (progress != null) return progress;

        progress = new CampaignProgress { UserId = userId };
        await _context.CampaignProgresses.AddAsync(progress);
        await _context.SaveChangesAsync();

        return progress;
    }

    public async Task<CampaignProgress?> GetProgressAsync(Guid userId)
    {
        return await _context.CampaignProgresses
            .FirstOrDefaultAsync(p => p.UserId == userId);
    }

    public async Task UpdateProgressAsync(CampaignProgress progress)
    {
        _context.CampaignProgresses.Update(progress);
        await _context.SaveChangesAsync();
    }
}

