using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Infrastructure.Data;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Infrastructure;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers everything database-related. The Desktop project calls this one
    /// method and never sees Npgsql, connection strings or EF Core directly.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Scoped DbContext: one per operation (each ViewModel action opens its own scope).
        // No EnableRetryOnFailure on purpose: BR-18 forbids silently retrying a write
        // whose commit outcome may be unknown.
        services.AddDbContext<EquipmentBorrowingDbContext>(options =>
            options.UseNpgsql(DatabaseConnection.GetConnectionString()));

        services.AddScoped<IStudentRepository, EfStudentRepository>();
        services.AddScoped<IEquipmentRepository, EfEquipmentRepository>();
        services.AddScoped<IBorrowingRepository, EfBorrowingRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IDatabaseInitializer, DatabaseInitializer>();

        return services;
    }
}