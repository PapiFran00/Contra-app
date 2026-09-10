using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace ContraApp.Services;
public sealed class CurrentUser(IHttpContextAccessor accessor, IDataProtectionProvider dataProtectionProvider)
{
    private readonly HttpContext? _context = accessor.HttpContext;
    private readonly IDataProtector _accessTokenProtector = dataProtectionProvider.CreateProtector("ContraApp.Auth.AccessToken.v1");

    public string? Token
    {
        get
        {
            var protectedToken = _context?.Request.Cookies["contra_access_token"];
            if (string.IsNullOrWhiteSpace(protectedToken)) return null;

            try { return _accessTokenProtector.Unprotect(protectedToken); }
            catch (CryptographicException) { return null; }
        }
    }
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
