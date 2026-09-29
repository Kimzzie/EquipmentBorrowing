using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using EquipmentBorrowing.Application.Interfaces;
using EquipmentBorrowing.Application.Services;
using EquipmentBorrowing.Desktop.ViewModels;
using EquipmentBorrowing.Infrastructure.Data;
using EquipmentBorrowing.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EquipmentBorrowing.Desktop;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();

        // Scoped: one DbContext per operation. ViewModels create a new
        // scope for every Load/Borrow/Return (see IServiceScopeFactory
        // usage below), so all repositories used within ONE operation
        // share the SAME context, and no operation reuses a stale one
        // left over from an earlier click.
        services.AddDbContext<EquipmentBorrowingDbContext>(options =>
            options.UseSqlite(DatabasePathProvider.GetConnectionString()));

        services.AddScoped<IStudentRepository, EfStudentRepository>();
        services.AddScoped<IEquipmentRepository, EfEquipmentRepository>();
        services.AddScoped<IBorrowingRepository, EfBorrowingRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();

        services.AddScoped<BorrowEquipmentService>();
        services.AddScoped<ReturnEquipmentService>();

        // ViewModels live for the whole app session, so they hold a scope
        // factory instead of holding repositories/services directly —
        // otherwise they'd capture one DbContext for the entire session,
        // exactly the staleness problem this design avoids.
        services.AddTransient<EquipmentViewModel>();
        services.AddTransient<BorrowingsViewModel>();
        services.AddTransient<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();

        // Apply any pending migrations and seed starting data on launch,
        // so a fresh clone of this repo doesn't need a manual `dotnet ef`
        // step before the app can run.
        using (var startupScope = provider.CreateScope())
        {
            var context = startupScope.ServiceProvider.GetRequiredService<EquipmentBorrowingDbContext>();
            context.Database.Migrate();
            DbSeeder.SeedAsync(context).GetAwaiter().GetResult();
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindowViewModel = provider.GetRequiredService<MainWindowViewModel>();

            desktop.MainWindow = new MainWindow
            {
                DataContext = mainWindowViewModel
            };

            _ = mainWindowViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}