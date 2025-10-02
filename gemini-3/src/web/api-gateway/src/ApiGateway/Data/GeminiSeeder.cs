using ApiGateway.Models;
using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;

public class GeminiSeeder
{
    public static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Admin", "User", "Manager" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole { Name = role });
            }

        }
    }
    public static async Task SeedAdminAsync(UserManager<User> userManager)
    {
        var adminUser = await userManager.FindByNameAsync("admin");
        if (adminUser == null)
        {
            var user = new User {
                UserName = "admin",
                Email = "admin@admin.com",
                EmailConfirmed = true,
                Role = "Admin"
            };
            await userManager.CreateAsync(user, "Admin1!");
        }
    }
}