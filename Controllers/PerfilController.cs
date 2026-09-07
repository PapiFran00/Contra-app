using ContraApp.Services;
using Microsoft.AspNetCore.Mvc;
namespace ContraApp.Controllers;
public sealed class PerfilController(CurrentUser current) : Controller { public IActionResult Index() { ViewBag.IsAuthenticated = current.IsAuthenticated; return View(); } }
