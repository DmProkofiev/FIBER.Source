using FIBER.Source.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FIBER.Source.Infrastructure.Data;

/// <summary>Создаёт контекст с актуальным ключом, удерживая доступ до его освобождения.</summary>
public sealed class RuntimeDbContextFactory(
    IDatabaseService database, DatabaseAccessCoordinator access, ILoggerFactory loggerFactory)
    : IDbContextFactory<FiberSourceDbContext>
{
    public FiberSourceDbContext CreateDbContext() =>
        throw new NotSupportedException("Используйте CreateDbContextAsync.");

    public async Task<FiberSourceDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
    {
        var lease = await access.AcquireAsync(ct: cancellationToken).ConfigureAwait(false);
        try
        {
            var connection = new SqliteConnectionStringBuilder
            {
                DataSource = database.DatabasePath,
                Password = database.CurrentPassword,
                Mode = SqliteOpenMode.ReadWrite,
                Cache = SqliteCacheMode.Private,
                ForeignKeys = true
            };
            var options = new DbContextOptionsBuilder<FiberSourceDbContext>()
                .UseSqlite(connection.ToString(), sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery))
                .UseLoggerFactory(loggerFactory)
                .Options;
            return new FiberSourceDbContext(options, lease);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }
}
