using APFlow.Application.Features.Invoices;
using APFlow.Application.Features.Suppliers;
using APFlow.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace APFlow.Application;

/// <summary>
/// Registers services owned by the Application layer. Called once from the composition
/// root (APFlow.Api's Program.cs).
/// </summary>
public static class DependencyInjection
{
    /// <summary>Registers Application-layer services into the DI container.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Scoped: these depend (transitively, via Infrastructure's repository
        // registrations) on the scoped AppDbContext.
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<ISupplierService, SupplierService>();

        // Further feature registrations, validators, and mapping profiles are added
        // here as they are implemented.
        return services;
    }
}
