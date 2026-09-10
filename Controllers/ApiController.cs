using ContraApp.Models;
using ContraApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ContraApp.Controllers;
[ApiController, Route("api")]
public sealed class ApiController(SupabaseGateway db, CurrentUser current, IOptions<SupabaseOptions> options) : ControllerBase
{
    private static readonly TimeZoneInfo ComplejoTimeZone = GetComplejoTimeZone();
    private static TimeZoneInfo GetComplejoTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Argentina/Buenos_Aires"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Argentina Standard Time"); }
    }
    private async Task<Usuario> Me() => await db.One<Usuario>("Usuarios", $"?id=eq.{current.Id}");
    private async Task<bool> Role(string role) => (await Me()).rol == role;
    [HttpGet("realtime-config")] public IActionResult RealtimeConfig() => current.IsAuthenticated ? Ok(new { url = options.Value.Url, anonKey = options.Value.AnonKey, accessToken = current.Token }) : Unauthorized();
    [HttpGet("inicio")] public async Task<IActionResult> Inicio() { var partidos = await db.Get<Partido>("partidos", "?estado=eq.publicado&order=fecha_hora.asc&limit=10"); return Ok(partidos); }
    [HttpPost("solicitudes-complejo")] public async Task<IActionResult> SolicitarComplejo(SolicitudComplejoRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.NombreComplejo) || string.IsNullOrWhiteSpace(input.Telefono) || (!input.TieneFutbol && !input.TienePadel) || (input.TieneFutbol && input.CanchasFutbol < 1) || (input.TienePadel && input.CanchasPadel < 1)) return BadRequest("Completá los datos del complejo y al menos una cancha.");
        await db.Insert<object>("solicitudes_complejo", new { usuario_id = current.IsAuthenticated ? current.Id : (Guid?)null, nombre_complejo = input.NombreComplejo.Trim(), tiene_futbol = input.TieneFutbol, canchas_futbol = input.CanchasFutbol, futbol_techada = input.FutbolTechada, tiene_padel = input.TienePadel, canchas_padel = input.CanchasPadel, padel_techada = input.PadelTechada, telefono = input.Telefono.Trim() }); return Ok();
    }
    [HttpPost("opiniones")] public async Task<IActionResult> Opinar(OpinionRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.Mensaje) || input.Mensaje.Length > 2000) return BadRequest("Escribí una opinión de hasta 2000 caracteres.");
        await db.Insert<object>("opiniones", new { usuario_id = current.IsAuthenticated ? current.Id : (Guid?)null, tipo = input.Tipo is "Error" or "Mejora" ? input.Tipo : "Opinión", mensaje = input.Mensaje.Trim() }); return Ok();
    }
    [HttpGet("complejos")] public async Task<IActionResult> Complejos([FromQuery] string? deporte) => Ok(await db.Get<Complejo>("complejos", "?activo=eq.true&order=nombre"));
    [HttpGet("canchas")] public async Task<IActionResult> Canchas(Guid complejoId, string deporte) => Ok(await db.Get<Cancha>("canchas", $"?complejo_id=eq.{complejoId}&deporte=eq.{Uri.EscapeDataString(deporte)}&activa=eq.true&order=nombre"));
    [HttpPost("partidos")] public async Task<IActionResult> CrearPartido(CrearPartidoRequest input)
    { if (!current.IsAuthenticated) return Unauthorized(); var me = await Me(); if (me.rol != "jugador") return Forbid(); var horarioLocal = TimeZoneInfo.ConvertTime(input.FechaHora, ComplejoTimeZone).TimeOfDay; if (input.Deporte is not ("Fútbol" or "Pádel") || input.Nivel is not ("Principiante" or "Intermedio" or "Avanzado") || (input.Deporte == "Pádel" && input.ModalidadPadel is not ("Mixto" or "Hombre" or "Mujer")) || (input.Deporte != "Pádel" && input.ModalidadPadel is not null) || input.FechaHora <= DateTimeOffset.UtcNow || horarioLocal.Minutes != 0 || horarioLocal.Hours is < 16 or > 23) return BadRequest("Elegí un turno válido entre las 16:00 y las 23:00."); var cancha = await db.One<Cancha>("canchas", $"?id=eq.{input.CanchaId}&complejo_id=eq.{input.ComplejoId}&deporte=eq.{input.Deporte}&activa=eq.true"); await db.One<Complejo>("complejos", $"?id=eq.{input.ComplejoId}&activo=eq.true"); var p = await db.Insert<Partido>("partidos", new { creador_id = current.Id, complejo_id = input.ComplejoId, cancha_id = cancha.id, deporte = input.Deporte, fecha_hora = input.FechaHora, nivel = input.Nivel, modalidad_padel = input.ModalidadPadel, estado = "publicado", notas = input.Notas }); return Ok(p); }
    [HttpGet("partidos")] public async Task<IActionResult> Buscar([FromQuery] string? deporte, [FromQuery] DateOnly? fecha)
    { var q = "?estado=eq.publicado&order=fecha_hora.asc"; if (!string.IsNullOrWhiteSpace(deporte)) q += "&deporte=eq." + Uri.EscapeDataString(deporte); if (fecha is not null) q += "&fecha_hora=gte." + fecha + "T00:00:00Z&fecha_hora=lt." + fecha.Value.AddDays(1) + "T00:00:00Z"; return Ok(await db.Get<Partido>("partidos", q)); }
    [HttpPost("partidos/{id:guid}/postulaciones")] public async Task<IActionResult> Postular(Guid id) { if (!current.IsAuthenticated) return Unauthorized(); var p = await db.Insert<Postulacion>("postulaciones", new { partido_id = id, jugador_id = current.Id, estado = "pendiente" }); return Ok(p); }
    [HttpPatch("postulaciones/{id:guid}")] public async Task<IActionResult> Resolver(Guid id, [FromQuery] string estado) { if (!current.IsAuthenticated) return Unauthorized(); if (estado is not ("aceptada" or "rechazada")) return BadRequest(); await db.Patch("postulaciones", $"?id=eq.{id}", new { estado }); return NoContent(); }
    [HttpGet("perfil")] public async Task<IActionResult> Perfil() => !current.IsAuthenticated ? Unauthorized() : Ok(await Me());
    [HttpPatch("perfil")] public async Task<IActionResult> Perfil(PerfilRequest input) { if (!current.IsAuthenticated) return Unauthorized(); await db.Patch("Usuarios", $"?id=eq.{current.Id}", new { nombre = input.Nombre, apellido = input.Apellido, fecha_nacimiento = input.FechaNacimiento, genero = input.Genero }); return NoContent(); }
    [HttpPost("perfil/avatar"), RequestSizeLimit(6 * 1024 * 1024)] public async Task<IActionResult> ActualizarAvatar(IFormFile? avatar)
    {
        if (!current.IsAuthenticated) return Unauthorized();
        if (avatar is null || avatar.Length == 0 || avatar.Length > 5 * 1024 * 1024 || avatar.ContentType != "image/jpeg") return BadRequest("La foto recortada debe ser un JPEG de hasta 5 MB.");
        await using var image = avatar.OpenReadStream();
        await db.UploadAvatar(current.Id, image, current.Token);
        var avatarUrl = db.PublicAvatarUrl(current.Id);
        await db.Patch("Usuarios", $"?id=eq.{current.Id}", new { avatar_url = avatarUrl });
        return Ok(new { avatarUrl });
    }
    [HttpPost("dueno/admins")] public async Task<IActionResult> AltaAdmin(AltaAdminRequest input)
    { if (!current.IsAuthenticated) return Unauthorized(); if (!await Role("dueno")) return Forbid(); var id = await db.CreateAuthUser(input); var user = await db.Insert<Usuario>("Usuarios", new { id, nombre = input.NombreComplejo, rol = "admin_complejo" }, true); var complejo = await db.Insert<Complejo>("complejos", new { administrador_id = id, nombre = input.NombreComplejo, ciudad = "Sunchales", activo = true }, true); return Ok(new { user, complejo }); }
    [HttpGet("admin/complejo")] public async Task<IActionResult> MiComplejo() => !current.IsAuthenticated ? Unauthorized() : Ok(await db.One<Complejo>("complejos", $"?administrador_id=eq.{current.Id}"));
    [HttpPut("admin/complejo/configuracion")] public async Task<IActionResult> Configurar(ConfigurarComplejoRequest input)
    { if (!current.IsAuthenticated) return Unauthorized(); if (!await Role("admin_complejo")) return Forbid(); if (input.FutbolTechadas > input.Futbol || input.PadelTechadas > input.Padel || input.Apertura >= input.Cierre) return BadRequest("Configuración inválida."); var complejo = await db.One<Complejo>("complejos", $"?administrador_id=eq.{current.Id}"); await db.Rpc("regenerar_canchas", new { p_complejo_id = complejo.id, p_futbol = input.Futbol, p_futbol_techadas = input.FutbolTechadas, p_padel = input.Padel, p_padel_techadas = input.PadelTechadas }); await db.Patch("complejos", $"?id=eq.{complejo.id}", new { horario_apertura = input.Apertura, horario_cierre = input.Cierre }); return NoContent(); }
    [HttpGet("chats")] public async Task<IActionResult> Chats() => !current.IsAuthenticated ? Unauthorized() : Ok(await db.Get<Conversacion>("conversaciones", "?order=creado_en.desc"));
    [HttpGet("chats/{id:guid}/mensajes")] public async Task<IActionResult> Mensajes(Guid id) => !current.IsAuthenticated ? Unauthorized() : Ok(await db.Get<Mensaje>("mensajes", $"?conversacion_id=eq.{id}&order=creado_en.asc"));
    [HttpPost("chats/{id:guid}/mensajes")] public async Task<IActionResult> Enviar(Guid id, MensajeRequest input) { if (!current.IsAuthenticated) return Unauthorized(); if (string.IsNullOrWhiteSpace(input.Contenido)) return BadRequest(); return Ok(await db.Insert<Mensaje>("mensajes", new { conversacion_id = id, remitente_id = current.Id, contenido = input.Contenido.Trim() })); }
}
