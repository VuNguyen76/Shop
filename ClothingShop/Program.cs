using ClothingShop.Data;
using ClothingShop.Services;
using ClothingShop.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Cryptography;
using System.Text;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);
var builder = WebApplication.CreateBuilder(args);

// POSTGRESQL
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Session + HttpContextAccessor
builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// QUAN TRỌNG: ĐĂNG KÝ CART SERVICE
builder.Services.AddScoped<ICartService, CartService>();

// ĐĂNG KÝ EMAIL SERVICE
builder.Services.AddScoped<IEmailService, EmailService>();

// ĐĂNG KÝ VNPAY SERVICE
builder.Services.AddScoped<IVNPayService, VNPayService>();

// AUTHENTICATION
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy =>
        policy.RequireRole("Admin"));

// MVC
builder.Services.AddControllersWithViews();

var app = builder.Build();

// HÀM MÃ HÓA
static string HashPassword(string password)
{
    return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
}

// TỰ ĐỘNG TẠO DB + ADMIN
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // Dùng EnsureCreated thay cho Migrate để tự tạo bảng trực tiếp vào CSDL mà không cần file Migration
    db.Database.EnsureCreated();

    var adminEmail = "gamer957ola@gmail.com";
    if (!await db.Users.AnyAsync(u => u.Email.ToLower() == adminEmail.ToLower()))
    {
        var admin = new User
        {
            FullName = "Admin Khang",
            Email = adminEmail,
            PasswordHash = HashPassword("Khang@123"),
            PhoneNumber = "0901234567",
            Gender = "Nam",
            IsAdmin = true,
            CreatedAt = DateTime.Now
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync();
    }
}

// Middleware
app.UseHttpsRedirection();

// Cấu hình MIME type cho AVIF
var provider = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider();
provider.Mappings[".avif"] = "image/avif";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = provider
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseSession();

// Middleware để ngăn cache cho các trang authenticated
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true || 
        context.Session.GetString("UserId") != null)
    {
        context.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        context.Response.Headers.Pragma = "no-cache";
        context.Response.Headers.Expires = "0";
    }
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();