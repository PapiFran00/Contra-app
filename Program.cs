using ContraApp.Services;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddUserSecrets<Program>(optional: true);
var dataProtectionKeysPath = builder.Configuration["DataProtection:KeyRingPath"]
    ?? Environment.GetEnvironmentVariable("DATA_PROTECTION_KEYS_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "work", "data-protection-keys");
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection()
    .SetApplicationName("ContraApp")
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysPath));
builder.Services.AddHttpClient();
builder.Services.Configure<SupabaseOptions>(builder.Configuration.GetSection("Supabase"));
builder.Services.AddOptions<SupabaseOptions>()
    .Validate(x => Uri.TryCreate(x.Url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps, "Supabase:Url debe ser una URL HTTPS válida.")
    .Validate(x => !string.IsNullOrWhiteSpace(x.AnonKey) && x.AnonKey.StartsWith("eyJ", StringComparison.Ordinal), "Supabase:AnonKey debe contener la clave legacy anon completa (eyJ...).")
    .ValidateOnStart();
builder.Services.AddScoped<SupabaseGateway>();
builder.Services.AddScoped<CurrentUser>();
var app = builder.Build();
app.UseExceptionHandler("/Home/Error");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.Use(async (context, next) => { context.Response.Headers.CacheControl = "no-store"; await next(); });
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
