using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using AlAdeeb.Models;

namespace AlAdeeb.Data
{
    public static class DbSeeder
    {
        public static void SeedAdmin(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                context.Database.EnsureCreated();

                if (!context.Users.Any(u => u.Role == "Admin"))
                {
                    var adminUser = new ApplicationUser
                    {
                        FullName = "أ. صلاح عبد العال",
                        Username = "admin",
                        PhoneNumber = "0500000000",
                        Role = "Admin",
                        CreatedAt = DateTime.Now
                    };

                    var passwordHasher = new PasswordHasher<ApplicationUser>();
                    adminUser.PasswordHash = passwordHasher.HashPassword(adminUser, "Admin@100");

                    context.Users.Add(adminUser);
                    context.SaveChanges();
                }
            }
        }
    }
}