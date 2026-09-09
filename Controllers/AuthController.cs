using ContraApp.Models;
using ContraApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ContraApp.Controllers;

public sealed class AuthController(SupabaseGateway db, IOptions<SupabaseOptions> options, IWebHostEnvironment environment) : Controller
{
    public IActionResult Login() => View();

    [HttpGet] 
    public IActionResult Registro() => View();

    [HttpGet] 
    public IActionResult Recuperar() => View();

    [HttpPost] 
    public async Task<IActionResult> Login(LoginRequest input)
    {
        try 
        { 
            var result = await db.Login(input); 
            
            // Extracción segura del access_token sin importar cómo venga estructurado el JSON de Supabase
            string? accessToken = null;
            if (result.TryGetProperty("access_token", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String)
            {
                accessToken = tokenProp.GetString();
            }
            else if (result.TryGetProperty("session", out var sessionProp) && sessionProp.TryGetProperty("access_token", out var innerToken))
            {
                accessToken = innerToken.GetString();
            }

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                return BadRequest("No se pudo obtener el token de acceso de Supabase.");
            }

            SetSession(accessToken); 
            return Ok(); 
        }
        catch (Exception ex) 
        { 
            return BadRequest("Credenciales inválidas o error de conexión: " + ex.Message); 
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
        var email = input.Correo.Trim(); 
        var nombre = Capitalizar(input.Nombre); 
        var apellido = Capitalizar(input.Apellido);

        if (string.IsNullOrWhiteSpace(email) || !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(email)) 
            return BadRequest("Ingresá un correo electrónico válido.");

        if (nombre.Length < 2 || apellido.Length < 2 || input.FechaNacimiento is null || input.FechaNacimiento > DateOnly.FromDateTime(DateTime.UtcNow) || input.Genero is not ("Hombre" or "Mujer" or "Prefiero no decirlo") || input.Contrasena.Length < 8) 
            return BadRequest("Completá los datos personales y usá una contraseña de al menos 8 caracteres.");

        try
        {
            if (input.Avatar is { Length: > 5 * 1024 * 1024 } || input.Avatar is { ContentType: not "image/jpeg" })
                return BadRequest("La foto recortada debe ser un JPEG de hasta 5 MB.");

            // 1. Registramos en Supabase Auth
            JsonElement signup;
            try
            {
                signup = await db.SignUp(new RegistroRequest { Correo = email, Contrasena = input.Contrasena }, PublicUrl("Auth/Confirmado"));
            }
            catch
            {
                return BadRequest("No se pudo registrar el usuario en Supabase. Es posible que el correo ya esté en uso.");
            }

            // 2. Obtenemos el ID de usuario y el token de manera ultra segura (o haciendo login si viene plano)
            string? accessToken = null;
            Guid id;

            if (signup.TryGetProperty("user", out var userElement) && userElement.ValueKind != JsonValueKind.Null && userElement.TryGetProperty("id", out var idProp))
            {
                id = Guid.Parse(idProp.GetString()!);
                if (signup.TryGetProperty("access_token", out var tokenProp) && tokenProp.ValueKind == JsonValueKind.String)
                {
                    accessToken = tokenProp.GetString();
                }
            }
            else
            {
                // Si el formato de respuesta del signUp no trae el usuario directo, nos logueamos al instante
                var loginFallback = await db.Login(new LoginRequest { Correo = email, Contrasena = input.Contrasena });
                var userObj = loginFallback.GetProperty("user");
                id = Guid.Parse(userObj.GetProperty("id").GetString()!);
                accessToken = loginFallback.GetProperty("access_token").GetString()!;
            }

            // Si por alguna razón todavía no hay token, hacemos un login rápido para asegurarlo
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                var forcedLogin = await db.Login(new LoginRequest { Correo = email, Contrasena = input.Contrasena });
                accessToken = forcedLogin.GetProperty("access_token").GetString()!;
            }

            // 3. Subimos el avatar si el usuario adjuntó uno
            string? avatarUrl = null;
            if (input.Avatar is { Length: > 0 })
            {
                await using var image = input.Avatar.OpenReadStream();
                await db.UploadAvatar(id, image, accessToken);
                avatarUrl = db.PublicAvatarUrl(id);
            }

            // 4. Guardamos el perfil en la tabla 'usuarios'
            var profile = new { id, nombre, apellido, fecha_nacimiento = input.FechaNacimiento, genero = input.Genero == "Prefiero no decirlo" ? null : input.Genero, rol = "jugador", avatar_url = avatarUrl };
            
            try
            {
                await db.InsertAsUser<Usuario>("usuarios", profile, accessToken);
            }
            catch
            {
                // Si falla como usuario por políticas de RLS, intentamos con la llave de servicio si está disponible
                await db.Insert<Usuario>("usuarios", profile, service: true);
            }

            // 5. Seteamos la cookie de sesión y dejamos entrar a la app
            SetSession(accessToken);
            return Ok();
        }
        catch (Exception ex) 
        { 
            return BadRequest("Error en el proceso de registro: " + ex.Message); 
        }
    }

    [HttpPost] 
    public IActionResult Salir() 
    { 
        Response.Cookies.Delete("contra_access_token"); 
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

    private void SetSession(string token) => Response.Cookies.Append("contra_access_token", token, new CookieOptions { HttpOnly = true, Secure = !environment.IsDevelopment(), SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromHours(8) });

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