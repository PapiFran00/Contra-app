using ContraApp.Models;
using ContraApp.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ContraApp.Controllers;

public sealed class AuthController(
    SupabaseGateway db,
    IOptions<SupabaseOptions> options,
    IDataProtectionProvider dataProtectionProvider,
    IWebHostEnvironment environment) : Controller
{
    private const string AccessTokenCookie = "contra_access_token";
    private readonly IDataProtector _accessTokenProtector = dataProtectionProvider.CreateProtector("ContraApp.Auth.AccessToken.v1");

    public IActionResult Login() => View();

    [HttpGet] 
    public IActionResult Registro() => View();

    [HttpGet] 
    public IActionResult Recuperar() => View();

    [HttpPost]
    public async Task<IActionResult> Login(LoginRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.Correo) || string.IsNullOrWhiteSpace(input.Contrasena))
            return BadRequest("Ingresá tu correo y contraseña.");

        try
        {
            var result = await db.Login(input);
            var accessToken = GetAccessToken(result);

            if (string.IsNullOrWhiteSpace(accessToken))
                return BadRequest("Supabase no devolvió una sesión válida.");

            SetSession(accessToken);
            return Ok();
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "No se pudo conectar con el servicio de autenticación.");
        }
        catch (InvalidOperationException)
        {
            // Los detalles devueltos por Supabase pueden contener información que no debe exponerse.
            return BadRequest("Correo o contraseña incorrectos.");
        }
    }

    [HttpGet] 
    public IActionResult Confirmado() => View();

    [HttpGet] 
    public IActionResult VerifyEmailNotice() => View();

    [HttpPost] 
    [RequestSizeLimit(6 * 1024 * 1024)] 
    public async Task<IActionResult> Registro([FromForm] CompletarRegistroRequest input)
    {
        var email = input.Correo.Trim().ToLowerInvariant();
        var nombre = Capitalizar(input.Nombre);
        var apellido = Capitalizar(input.Apellido);

        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email))
            return BadRequest("Ingresá un correo electrónico válido.");

        if (nombre.Length < 2 || apellido.Length < 2 || input.FechaNacimiento is null || input.FechaNacimiento > DateOnly.FromDateTime(DateTime.UtcNow) || input.Genero is not ("Hombre" or "Mujer" or "Prefiero no decirlo") || input.Contrasena.Length < 8)
            return BadRequest("Completá los datos personales y usá una contraseña de al menos 8 caracteres.");

        if (!string.Equals(input.Contrasena, input.ConfirmarContrasena, StringComparison.Ordinal))
            return BadRequest("Las contraseñas no coinciden.");

        try
        {
            if (input.Avatar is { Length: > 5 * 1024 * 1024 } || input.Avatar is { ContentType: not "image/jpeg" })
                return BadRequest("La foto recortada debe ser un JPEG de hasta 5 MB.");

            // La API administrativa usa ServiceRoleKey y deja email_confirm=true.
            var userId = await db.CreateAuthUser(new AltaAdminRequest { Correo = email, Contrasena = input.Contrasena });

            string? avatarUrl = null;
            if (input.Avatar is { Length: > 0 })
            {
                await using var image = input.Avatar.OpenReadStream();
                await db.UploadAvatar(userId, image, null);
                avatarUrl = db.PublicAvatarUrl(userId);
            }

            var profile = new { id = userId, nombre, apellido, fecha_nacimiento = input.FechaNacimiento, genero = input.Genero == "Prefiero no decirlo" ? null : input.Genero, rol = "jugador", avatar_url = avatarUrl };

            // PostgREST distingue mayúsculas: el nombre real es exactamente "Usuarios".
            await db.Insert<Usuario>("Usuarios", profile, service: true);

            return Ok();
        }
        catch (HttpRequestException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "No se pudo conectar con el servicio de registro.");
        }
        catch (InvalidOperationException)
        {
            return BadRequest("No fue posible completar el registro. El correo podría estar en uso.");
        }
    }

    [HttpPost] 
    public IActionResult Salir() 
    { 
        Response.Cookies.Delete(AccessTokenCookie, CookieOptions());
        return Ok(); 
    }

    [HttpPost] 
    public async Task<IActionResult> RecuperarContrasena(RecuperarContrasenaRequest input) 
    { 
        try 
        { 
            await db.RecoverPassword(input, PublicUrl("Auth/Login")); 
            return Ok(); 
        } 
        catch 
        { 
            return BadRequest("No fue posible iniciar la recuperación."); 
        } 
    }

    private void SetSession(string token)
    {
        var cookieOptions = CookieOptions();
        cookieOptions.MaxAge = GetTokenLifetime(token) ?? TimeSpan.FromHours(8);
        Response.Cookies.Append(AccessTokenCookie, _accessTokenProtector.Protect(token), cookieOptions);
    }

    private CookieOptions CookieOptions() => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Path = "/"
    };

    private static string? GetAccessToken(JsonElement result)
    {
        if (result.TryGetProperty("access_token", out var token) && token.ValueKind == JsonValueKind.String)
            return token.GetString();

        return result.TryGetProperty("session", out var session)
            && session.ValueKind == JsonValueKind.Object
            && session.TryGetProperty("access_token", out token)
            && token.ValueKind == JsonValueKind.String
                ? token.GetString()
                : null;
    }

    private static TimeSpan? GetTokenLifetime(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;
            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            if (!document.RootElement.TryGetProperty("exp", out var exp) || !exp.TryGetInt64(out var unixSeconds)) return null;

            var lifetime = DateTimeOffset.FromUnixTimeSeconds(unixSeconds) - DateTimeOffset.UtcNow;
            return lifetime > TimeSpan.Zero ? lifetime : null;
        }
        catch
        {
            return null;
        }
    }

    private string PublicUrl(string path)
    {
        var configured = options.Value.PublicAppUrl;
        var baseUrl = !string.IsNullOrWhiteSpace(configured) ? configured : $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        return new Uri(new Uri(baseUrl.TrimEnd('/') + "/"), path).ToString();
    }

    private static string Capitalizar(string value) 
    { 
        var clean = value.Trim(); 
        return clean.Length == 0 ? clean : char.ToUpperInvariant(clean[0]) + clean[1..]; 
    }
}
