namespace ContraApp.Models;
public sealed record HeaderViewModel(bool IsAuthenticated, string? Nombre, string? Apellido, string? AvatarUrl)
{
    // El fallback nunca queda vacío aunque el perfil aún esté incompleto.
    public string Initials => string.Concat((Nombre ?? "").Trim().Take(1), (Apellido ?? "").Trim().Take(1)).ToUpperInvariant() is { Length: > 0 } initials ? initials : "U";
}
public sealed class SolicitudComplejoRequest { public string NombreComplejo { get; set; } = ""; public bool TieneFutbol { get; set; } public int CanchasFutbol { get; set; } public bool FutbolTechada { get; set; } public bool TienePadel { get; set; } public int CanchasPadel { get; set; } public bool PadelTechada { get; set; } public string Telefono { get; set; } = ""; }
public sealed class OpinionRequest { public string Mensaje { get; set; } = ""; public string? Tipo { get; set; } }
public sealed class RecuperarContrasenaRequest { public string Correo { get; set; } = ""; }
public sealed class CompletarRegistroRequest
{
    public string Correo { get; set; } = "";
    public string Nombre { get; set; } = "";
    public string Apellido { get; set; } = "";
    public DateOnly? FechaNacimiento { get; set; }
    public string? Genero { get; set; }
    public string Contrasena { get; set; } = "";
    public string ConfirmarContrasena { get; set; } = "";
    public IFormFile? Avatar { get; set; }
}
