using ContraApp.Services;
using Microsoft.AspNetCore.Mvc;
namespace ContraApp.Controllers;
public sealed class HomeController(CurrentUser current, SupabaseGateway db) : Controller
{
    public async Task<IActionResult> Index()
    {
        if (current.IsAuthenticated) { try { ViewBag.Nombre = (await db.One<ContraApp.Models.Usuario>("usuarios", $"?id=eq.{current.Id}")).nombre; } catch { } }
        return View();
    }
}
