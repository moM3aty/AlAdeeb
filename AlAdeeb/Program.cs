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

// 1. إعداد قاعدة البيانات
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. إعداد الهوية والجلسة الواحدة (OnValidatePrincipal)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Home/Index";
        options.LogoutPath = "/Account/Logout";
        options.ExpireTimeSpan = System.TimeSpan.FromDays(30);

        // هذه هي النقطة الجوهرية: التحقق من الجلسة في كل طلب
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async context =>
            {
                var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var sessionId = context.Principal?.FindFirstValue("SessionId");

                if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(sessionId))
                {
                    var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

                    // البحث عن المستخدم في قاعدة البيانات والتأكد من تطابق معرّف الجلسة
                    var userExists = await db.Users
                        .AsNoTracking()
                        .Where(u => u.Id == int.Parse(userId) && u.CurrentSessionId == sessionId)
                        .AnyAsync();

                    if (!userExists)
                    {
                        // إذا لم يتطابق، يتم طرده فوراً
                        context.RejectPrincipal();
                        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                }
            }
        };
    });

builder.Services.AddControllersWithViews();

var app = builder.Build();

// تهيئة بيانات المسؤول
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