using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.IO;

namespace FIBER.Source.Infrastructure.Data;

public class FiberSourceDbContextFactory : IDesignTimeDbContextFactory<FiberSourceDbContext>
{
    public FiberSourceDbContext CreateDbContext(string[] args)
    {
        var dbDir = Path.Combine(Directory.GetCurrentDirectory(), "Data");
        Directory.CreateDirectory(dbDir);
        var dbPath = Path.Combine(dbDir, "DesignTime.db");

        var options = new DbContextOptionsBuilder<FiberSourceDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        return new FiberSourceDbContext(options);
    }
}