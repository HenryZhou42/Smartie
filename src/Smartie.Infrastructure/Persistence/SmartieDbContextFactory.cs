using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Smartie.Infrastructure.Persistence;

/// <summary>
/// Design-time factory so <c>dotnet ef</c> can create the context (and generate
/// migrations) without booting the API host.
/// </summary>
public sealed class SmartieDbContextFactory : IDesignTimeDbContextFactory<SmartieDbContext>
{
    public SmartieDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SmartieDbContext>()
            .UseSqlite("Data Source=smartie.design.db")
            .Options;
        return new SmartieDbContext(options);
    }
}
