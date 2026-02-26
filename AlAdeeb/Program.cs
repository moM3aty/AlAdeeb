using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using AlAdeeb.Data;
using Microsoft.Extensions.Logging;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Home/Index";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = System.TimeSpan.FromDays(30);

        // المراقب الأمني للأجهزة المتعددة (يتم تنفيذه في كل طلب)
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionId = context.Principal?.FindFirstValue("SessionId");

                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(sessionId))
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                    var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == int.Parse(userId));

                    if (user != null)
                    {
                        var activeSessions = string.IsNullOrEmpty(user.ActiveSessionsJson)
                            ? new System.Collections.Generic.List<string>()
                            : System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<string>>(user.ActiveSessionsJson);

                        // إذا كان الجلسة الحالية غير موجودة في قائمة الأجهزة النشطة للطالب، يتم طرده فوراً
                        if (!activeSessions.Contains(sessionId))
                        {
                            context.RejectPrincipal();
                            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        }
                    }
                    else
                    {
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                }
            }
        };
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

DbSeeder.SeedAdmin(app.Services);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();