using Microsoft.AspNetCore.Mvc;
namespace ContraApp.Controllers;
public sealed class PartidosController : Controller { public IActionResult Crear() => View(); public IActionResult Buscar() => View(); }
