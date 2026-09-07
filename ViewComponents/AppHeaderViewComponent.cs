using ContraApp.Models;
using ContraApp.Services;
using Microsoft.AspNetCore.Mvc;

namespace ContraApp.ViewComponents;
public sealed class AppHeaderViewComponent(CurrentUser current, SupabaseGateway db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!current.IsAuthenticated) return View(new HeaderViewModel(false, null, null, null));
        try { var user = await db.One<Usuario>("usuarios", $"?id=eq.{current.Id}"); return View(new HeaderViewModel(true, user.nombre, user.apellido, user.avatar_url)); }
        catch { return View(new HeaderViewModel(false, null, null, null)); }
    }
}
