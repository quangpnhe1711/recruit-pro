namespace RecruitPro.API.Seeding;

public static class V5SeedingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the v5 demo seeder ONLY in the Development environment. Demo/telemetry data must never
    /// be auto-inserted in Production or race integration tests (Testing), so this is a no-op elsewhere.
    /// </summary>
    public static IServiceCollection AddV5DemoSeeder(this IServiceCollection services, IWebHostEnvironment environment)
    {
        if (environment.IsDevelopment())
        {
            services.AddHostedService<V5DemoSeederHostedService>();
        }

        return services;
    }
}
