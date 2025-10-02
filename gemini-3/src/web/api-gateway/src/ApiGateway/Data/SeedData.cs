using Microsoft.AspNetCore.Identity;
using ApiGateway.Models;
using System;
using System.Threading.Tasks;

public static class SeedData
{
    public static async Task InitializeAsync(UserManager<GeminiUserModel> userManager, RoleManager<IdentityRole> roleManager)
    {
        const string adminRole = "Admin";
        const string adminUserName = "admin";
        const string adminEmail = "admin@crane.com";
        const string adminPassword = "Admin123!";

        if (!await roleManager.RoleExistsAsync(adminRole))
        {
            await roleManager.CreateAsync(new IdentityRole(adminRole));
        }

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new GeminiUserModel
            {
                UserName = adminUserName,
                Email = adminEmail,
            };
            var result = await userManager.CreateAsync(adminUser, adminPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, adminRole);
            }
            else
            {
                throw new Exception("Failed to create admin user: " + string.Join(", ", result.Errors));
            }
        }

        const string engRole = "Engineer";
        const string engUserName = "eng";
        const string engEmail = "eng@crane.com";
        const string engPassword = "Eng123!";

        if (!await roleManager.RoleExistsAsync(engRole))
        {
            await roleManager.CreateAsync(new IdentityRole(engRole));
        }

        var engUser = await userManager.FindByEmailAsync(engEmail);
        if (engUser == null)
        {
            engUser = new GeminiUserModel
            {
                UserName = engUserName,
                Email = engEmail,
            };
            var result = await userManager.CreateAsync(engUser, engPassword);
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(engUser, engRole);
            }
            else
            {
                throw new Exception("Failed to create admin user: " + string.Join(", ", result.Errors));
            }
        }
    }
}