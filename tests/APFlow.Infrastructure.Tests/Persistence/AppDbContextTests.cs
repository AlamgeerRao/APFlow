using APFlow.Application.Interfaces;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

public class AppDbContextTests
{
    [Fact]
    public void SaveChanges_OnAdd_StampsCreatedAtAndCreatedBy()
    {
        using var context = CreateContext(userId: "user-1", tenantId: Guid.NewGuid());
        var entity = new TestTenantEntity { Name = "Test" };

        context.TestEntities.Add(entity);
        context.SaveChanges();

        Assert.NotEqual(default, entity.CreatedAtUtc);
        Assert.Equal("user-1", entity.CreatedBy);
        Assert.Null(entity.ModifiedAtUtc);
    }

    [Fact]
    public void SaveChanges_OnAdd_StampsTenantId_WhenNotAlreadySet()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(userId: "user-1", tenantId: tenantId);
        var entity = new TestTenantEntity { Name = "Test" };

        context.TestEntities.Add(entity);
        context.SaveChanges();

        Assert.Equal(tenantId, entity.TenantId);
    }

    [Fact]
    public void SaveChanges_OnAdd_DoesNotOverwriteExplicitlySetTenantId()
    {
        var explicitTenantId = Guid.NewGuid();
        using var context = CreateContext(userId: "user-1", tenantId: Guid.NewGuid());
        var entity = new TestTenantEntity { Name = "Test", TenantId = explicitTenantId };

        context.TestEntities.Add(entity);
        context.SaveChanges();

        Assert.Equal(explicitTenantId, entity.TenantId);
    }

    [Fact]
    public void SaveChanges_NoCurrentUser_UsesSystemAsCreatedBy()
    {
        using var context = CreateContext(userId: null, tenantId: null);
        var entity = new TestTenantEntity { Name = "Test" };

        context.TestEntities.Add(entity);
        context.SaveChanges();

        Assert.Equal("system", entity.CreatedBy);
        Assert.Equal(Guid.Empty, entity.TenantId);
    }

    [Fact]
    public void SaveChanges_UnresolvableTenant_LogsWarning()
    {
        var logger = new CapturingLogger<AppDbContext>();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new TestAppDbContext(options, currentUserService: null, logger);
        var entity = new TestTenantEntity { Name = "Test" };

        context.TestEntities.Add(entity);
        context.SaveChanges();

        Assert.Contains(logger.Entries, e => e.LogLevel == LogLevel.Warning && e.Message.Contains("no resolvable tenant"));
    }

    [Fact]
    public void SaveChanges_OnModify_StampsModifiedAtAndModifiedBy()
    {
        using var context = CreateContext(userId: "user-1", tenantId: Guid.NewGuid());
        var entity = new TestTenantEntity { Name = "Original" };
        context.TestEntities.Add(entity);
        context.SaveChanges();

        entity.Name = "Updated";
        context.SaveChanges();

        Assert.NotNull(entity.ModifiedAtUtc);
        Assert.Equal("user-1", entity.ModifiedBy);
    }

    [Fact]
    public void Remove_ConvertsToSoftDelete_RowIsNotPhysicallyDeleted()
    {
        using var context = CreateContext(userId: "deleter", tenantId: Guid.NewGuid());
        var entity = new TestTenantEntity { Name = "ToDelete" };
        context.TestEntities.Add(entity);
        context.SaveChanges();

        context.TestEntities.Remove(entity);
        context.SaveChanges();

        Assert.True(entity.IsDeleted);
        Assert.NotNull(entity.DeletedAtUtc);
        Assert.Equal("deleter", entity.DeletedBy);
    }

    [Fact]
    public void SoftDeletedRow_IsExcludedFromNormalQueries_ByGlobalQueryFilter()
    {
        using var context = CreateContext(userId: "user-1", tenantId: Guid.NewGuid());
        var entity = new TestTenantEntity { Name = "Hidden" };
        context.TestEntities.Add(entity);
        context.SaveChanges();

        context.TestEntities.Remove(entity);
        context.SaveChanges();

        var visible = context.TestEntities.ToList();

        Assert.Empty(visible);
    }

    [Fact]
    public void SoftDeletedRow_IsStillReachable_WithIgnoreQueryFilters()
    {
        using var context = CreateContext(userId: "user-1", tenantId: Guid.NewGuid());
        var entity = new TestTenantEntity { Name = "StillThere" };
        context.TestEntities.Add(entity);
        context.SaveChanges();

        context.TestEntities.Remove(entity);
        context.SaveChanges();

        var stillInDatabase = context.TestEntities.IgnoreQueryFilters().ToList();

        Assert.Single(stillInDatabase);
        Assert.True(stillInDatabase[0].IsDeleted);
    }

    private static TestAppDbContext CreateContext(string? userId, Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        ICurrentUserService? currentUserService = userId is null && tenantId is null
            ? null
            : new FakeCurrentUserService(userId, tenantId);

        return new TestAppDbContext(options, currentUserService);
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(string? userId, Guid? tenantId)
        {
            UserId = userId;
            TenantId = tenantId?.ToString();
        }

        public bool IsAuthenticated => UserId is not null;
        public string? UserId { get; }
        public string? Email => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }

    /// <summary>Minimal ILogger test double that captures log entries for assertion.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel LogLevel, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
