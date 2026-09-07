using System.Text;
using System.Text.Json;

namespace ContraApp.Services;
public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    private readonly HttpContext? _context = accessor.HttpContext;
    public string? Token => _context?.Request.Cookies["contra_access_token"];
    public bool IsAuthenticated => !string.IsNullOrWhiteSpace(Token);
    public Guid Id
    {
        get
        {
            var token = Token ?? throw new UnauthorizedAccessException();
            var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var sub = JsonDocument.Parse(Encoding.UTF8.GetString(Convert.FromBase64String(payload))).RootElement.GetProperty("sub").GetString();
            return Guid.Parse(sub ?? throw new UnauthorizedAccessException());
        }
    }
}
