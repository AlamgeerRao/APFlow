using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises <see cref="InvoiceRepository.GetByIdWithNotesAsync"/> and
/// <see cref="InvoiceNote.AuthorDisplayName"/> (WP-055) against a real
/// <see cref="AppDbContext"/> (InMemory provider - same approach as
/// <see cref="InvoiceRepositoryQueryTests"/>), proving two things a fake
/// repository cannot: the new column actually round-trips through EF Core, and
/// <see cref="APFlow.Domain.Entities.AuditEntity.CreatedAtUtc"/> is genuinely
/// stamped by <c>SaveChangesAsync</c> (not set by test code - its setter is
/// <c>internal</c> to APFlow.Infrastructure, which this test project is not) so
/// notes can be correctly ordered most-recent-first, the way
/// <c>InvoiceService.GetNotesAsync</c> orders them.
/// </summary>
public class InvoiceNoteRepositoryTests
{
    [Fact]
    public async Task GetByIdWithNotesAsync_ReturnsNotes_WithAuthorDisplayNamePersisted_MostRecentFirstByCreatedAtUtc()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        var invoice = new Invoice { SupplierId = supplier.Id, TenantId = tenantId };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var first = new InvoiceNote { InvoiceId = invoice.Id, TenantId = tenantId, Content = "First note.", AuthorDisplayName = "Priya Shah" };
        context.InvoiceNotes.Add(first);
        await context.SaveChangesAsync(); // Stamps first.CreatedAtUtc.

        var second = new InvoiceNote { InvoiceId = invoice.Id, TenantId = tenantId, Content = "Second note.", AuthorDisplayName = "Patrick" };
        context.InvoiceNotes.Add(second);
        await context.SaveChangesAsync(); // Stamps second.CreatedAtUtc, strictly later.

        var loaded = await repository.GetByIdWithNotesAsync(invoice.Id);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded!.Notes.Count);

        var ordered = loaded.Notes.OrderByDescending(n => n.CreatedAtUtc).ToList();
        Assert.Equal(second.Id, ordered[0].Id);
        Assert.Equal("Patrick", ordered[0].AuthorDisplayName);
        Assert.Equal(first.Id, ordered[1].Id);
        Assert.Equal("Priya Shah", ordered[1].AuthorDisplayName);
        Assert.True(ordered[0].CreatedAtUtc > ordered[1].CreatedAtUtc);
    }

    [Fact]
    public async Task GetByIdWithNotesAsync_NoteWithNullAuthorDisplayName_PersistsAsNull()
    {
        // A caller's token may lack a name claim (see
        // CurrentUserService.DisplayName's own doc comment) - this must persist
        // and round-trip as null, not an empty string or a substituted value.
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        var invoice = new Invoice { SupplierId = supplier.Id, TenantId = tenantId };
        context.Invoices.Add(invoice);
        context.InvoiceNotes.Add(new InvoiceNote { InvoiceId = invoice.Id, TenantId = tenantId, Content = "Anonymous-ish note.", AuthorDisplayName = null });
        await context.SaveChangesAsync();

        var loaded = await repository.GetByIdWithNotesAsync(invoice.Id);

        Assert.NotNull(loaded);
        var note = Assert.Single(loaded!.Notes);
        Assert.Null(note.AuthorDisplayName);
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new FakeCurrentUserService(tenantId));
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid tenantId)
        {
            TenantId = tenantId.ToString();
        }

        public bool IsAuthenticated => true;
        public string? UserId => "test-user";
        public string? Email => null;
        public string? DisplayName => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
