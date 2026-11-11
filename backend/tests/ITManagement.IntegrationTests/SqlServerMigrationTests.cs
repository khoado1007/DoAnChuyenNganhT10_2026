using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

public class SqlServerMigrationTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Migrations_match_model_and_reapply_without_losing_data()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", db.Database.ProviderName);
        Assert.StartsWith("itmanagement_test_", db.Database.GetDbConnection().Database);
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Contains("20261110140837_InitialSqlServer", await db.Database.GetAppliedMigrationsAsync());
        var admin = await db.AppUsers.SingleAsync(u => u.Username == "admin");
        await db.Database.MigrateAsync();
        Assert.Equal(admin.Id, (await db.AppUsers.AsNoTracking().SingleAsync(u => u.Username == "admin")).Id);
    }
}

public class SqlServerModelTests
{
    [Fact]
    public void Snapshot_matches_runtime_model_without_connecting_to_user_database()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(@"Server=(localdb)\MSSQLLocalDB;Database=itmanagement_model_only;Integrated Security=True;Encrypt=False").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
