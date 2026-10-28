using FIBER.Source.Infrastructure.Data;
using FIBER.Source.Interfaces;
using FIBER.Source.Models;
using Microsoft.EntityFrameworkCore;

namespace FIBER.Source.Services;

public class CountryService : ICountryService
{
    private readonly IDbContextFactory<FiberSourceDbContext> _contextFactory;

    public CountryService(IDbContextFactory<FiberSourceDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Country>> GetActiveAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        return await context.Countries
            .AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.NameRu)
            .ToListAsync();
    }
}