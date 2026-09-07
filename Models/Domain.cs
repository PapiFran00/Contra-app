namespace ContraApp.Models;

public sealed record Usuario(Guid id, string nombre, string? apellido, DateOnly? fecha_nacimiento, string? genero, string rol, string? avatar_url);
public sealed record Complejo(Guid id, Guid administrador_id, string nombre, TimeOnly? horario_apertura, TimeOnly? horario_cierre, string ciudad, bool activo);
public sealed record Cancha(Guid id, Guid complejo_id, string nombre, string deporte, bool techada, bool activa);
public sealed record Partido(Guid id, Guid creador_id, Guid complejo_id, Guid cancha_id, string deporte, DateTimeOffset fecha_hora, string nivel, string? modalidad_padel, string estado, string? notas, DateTimeOffset creado_en);
public sealed record Postulacion(Guid id, Guid partido_id, Guid jugador_id, string estado, DateTimeOffset creado_en);
public sealed record Conversacion(Guid id, Guid partido_id, DateTimeOffset creado_en);
public sealed record Mensaje(Guid id, Guid conversacion_id, Guid remitente_id, string contenido, DateTimeOffset creado_en);

public sealed class CrearPartidoRequest { public Guid ComplejoId { get; set; } public Guid CanchaId { get; set; } public string Deporte { get; set; } = ""; public DateTimeOffset FechaHora { get; set; } public string Nivel { get; set; } = ""; public string? ModalidadPadel { get; set; } public string? Notas { get; set; } }
public sealed class ConfigurarComplejoRequest { public int Futbol { get; set; } public int FutbolTechadas { get; set; } public int Padel { get; set; } public int PadelTechadas { get; set; } public TimeOnly Apertura { get; set; } public TimeOnly Cierre { get; set; } }
public sealed class AltaAdminRequest { public string Correo { get; set; } = ""; public string Contrasena { get; set; } = ""; public string NombreComplejo { get; set; } = ""; }
public sealed class PerfilRequest { public string Nombre { get; set; } = ""; public string? Apellido { get; set; } public DateOnly? FechaNacimiento { get; set; } public string? Genero { get; set; } }
public sealed class MensajeRequest { public string Contenido { get; set; } = ""; }
public sealed class LoginRequest { public string Correo { get; set; } = ""; public string Contrasena { get; set; } = ""; }
public sealed class RegistroRequest { public string Nombre { get; set; } = ""; public string Correo { get; set; } = ""; public string Contrasena { get; set; } = ""; }
