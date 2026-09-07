using ContraApp.Services;
using Microsoft.AspNetCore.Mvc;
namespace ContraApp.Controllers;
public sealed class ChatController(CurrentUser current) : Controller { public IActionResult Index() { ViewBag.IsAuthenticated = current.IsAuthenticated; return View(); } public IActionResult Conversacion(Guid id) => current.IsAuthenticated ? View(id) : RedirectToAction("Index", "Perfil"); }
