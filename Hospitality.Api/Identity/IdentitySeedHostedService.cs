using Hospitality.Api.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Hospitality.Api.Identity;

public sealed class IdentitySeedHostedService(
    IServiceProvider services,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<IdentitySeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (db.Database.IsRelational())
        {
            await db.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
        }

        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var email = configuration["ADMIN_EMAIL"];
        var username = configuration["ADMIN_USERNAME"];
        var password = configuration["ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(email)
            || string.IsNullOrWhiteSpace(username)
            || string.IsNullOrWhiteSpace(password))
        {
            var anyAdmin = await userManager.GetUsersInRoleAsync(Roles.Admin);
            if (anyAdmin.Count > 0)
            {
                return;
            }

            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "ADMIN_EMAIL, ADMIN_USERNAME, and ADMIN_PASSWORD are required to seed the first Admin.");
            }

            logger.LogWarning("Skipping Admin seed; ADMIN_* settings are not configured.");
            return;
        }

        var existing = await userManager.FindByNameAsync(username);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, Roles.Admin))
            {
                await userManager.AddToRoleAsync(existing, Roles.Admin);
            }

            return;
        }

        var emailToUse = email;
        if (await userManager.FindByEmailAsync(email) is not null)
        {
            emailToUse = $"{username}@localhost";
        }

        var admin = new IdentityUser
        {
            UserName = username,
            Email = emailToUse,
            EmailConfirmed = true
        };

        var created = await userManager.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            throw new InvalidOperationException(
                $"Failed to seed Admin: {string.Join(", ", created.Errors.Select(e => e.Description))}");
        }

        await userManager.AddToRoleAsync(admin, Roles.Admin);
        logger.LogInformation("Seeded Admin user {Username}.", username);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
