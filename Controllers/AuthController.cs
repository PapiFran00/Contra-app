using ContraApp.Models;
using ContraApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

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
            SetSession(result.GetProperty("access_token").GetString()!); 
            return Ok(); 
        }
        catch 
        { 
            return BadRequest("Credenciales inválidas."); 
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

            var signup = await db.SignUp(new RegistroRequest { Correo = email, Contrasena = input.Contrasena }, PublicUrl("Auth/Confirmado"));
            
            // Verificación segura de la propiedad "user" para evitar errores de diccionario
            if (!signup.TryGetProperty("user", out var userElement))
                return BadRequest("No se pudo obtener la información del usuario desde Supabase.");

            var id = Guid.Parse(userElement.GetProperty("id").GetString()!);
            
            // Extracción segura del token de acceso
            string? accessToken = null;
            if (signup.TryGetProperty("access_token", out var tokenProp))
            {
                accessToken = tokenProp.GetString();
            }

            string? avatarUrl = null;
            if (input.Avatar is { Length: > 0 })
            {
                await using var image = input.Avatar.OpenReadStream();
                await db.UploadAvatar(id, image, accessToken);
                avatarUrl = db.PublicAvatarUrl(id);
            }

            var profile = new { id, nombre, apellido, fecha_nacimiento = input.FechaNacimiento, genero = input.Genero == "Prefiero no decirlo" ? null : input.Genero, rol = "jugador", avatar_url = avatarUrl };
            
            if (string.IsNullOrWhiteSpace(accessToken))
                await db.Insert<Usuario>("usuarios", profile, service: true);
            else
                await db.InsertAsUser<Usuario>("usuarios", profile, accessToken);

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                SetSession(accessToken);
            }

            return Ok(new { requiresEmailConfirmation = false });
        }
        catch (Exception ex) 
        { 
            return BadRequest(ex.Message); 
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