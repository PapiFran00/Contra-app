using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ContraApp.Models;
using Microsoft.Extensions.Options;

namespace ContraApp.Services;
public sealed class SupabaseOptions
{
    public string Url { get; set; } = "";
    public string AnonKey { get; set; } = "";
    public string ServiceRoleKey { get; set; } = "";
    // URL pública de la aplicación. En producción definirla como Supabase__PublicAppUrl.
    public string? PublicAppUrl { get; set; }
    public bool HasServiceRoleKey => !string.IsNullOrWhiteSpace(ServiceRoleKey) && ServiceRoleKey != "CONFIGURAR_SOLO_EN_USER-SECRETS";
}
public sealed class SupabaseGateway(IHttpClientFactory factory, IOptions<SupabaseOptions> options, CurrentUser current)
{
    private readonly SupabaseOptions _cfg = options.Value; private readonly CurrentUser _current = current;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower, PropertyNameCaseInsensitive = true };
    private HttpClient Client(bool service = false)
    {
        var c = factory.CreateClient();
        c.BaseAddress = new Uri(_cfg.Url.TrimEnd('/') + "/");
        var key = service ? _cfg.ServiceRoleKey : _cfg.AnonKey;
        if (string.IsNullOrWhiteSpace(key) || key == "CONFIGURAR_SOLO_EN_USER-SECRETS")
            throw new InvalidOperationException(service
                ? "Falta configurar Supabase:ServiceRoleKey en los secretos de usuario o en una variable de entorno."
                : "Falta configurar Supabase:AnonKey.");
        c.DefaultRequestHeaders.Add("apikey", key);
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", service ? key : (_current.Token ?? _cfg.AnonKey));
        return c;
    }
    private HttpClient ClientForToken(string accessToken)
    {
        var c = Client();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return c;
    }
    public async Task<List<T>> Get<T>(string table, string query = "") { var r = await Client().GetAsync("rest/v1/" + table + query); await Ok(r); return await r.Content.ReadFromJsonAsync<List<T>>(Json) ?? []; }
    public async Task<T> One<T>(string table, string query) => (await Get<T>(table, query)).First();
    public async Task<T> Insert<T>(string table, object body, bool service = false) { var c = Client(service); c.DefaultRequestHeaders.Add("Prefer", "return=representation"); var r = await c.PostAsJsonAsync("rest/v1/" + table, body, Json); await Ok(r); return (await r.Content.ReadFromJsonAsync<List<T>>(Json))!.First(); }
    // Durante el alta todavía no existe la cookie de la aplicación. Usamos el token que
    // Supabase acaba de emitir para que la política RLS "perfil propio" autorice el perfil.
    public async Task<T> InsertAsUser<T>(string table, object body, string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) throw new InvalidOperationException("Supabase no devolvió una sesión para la nueva cuenta.");
        var c = ClientForToken(accessToken);
        c.DefaultRequestHeaders.Add("Prefer", "return=representation");
        var r = await c.PostAsJsonAsync("rest/v1/" + table, body, Json);
        await Ok(r);
        return (await r.Content.ReadFromJsonAsync<List<T>>(Json))!.First();
    }
    public async Task Patch(string table, string query, object body) { var r = await Client().PatchAsJsonAsync("rest/v1/" + table + query, body, Json); await Ok(r); }
    public async Task Delete(string table, string query) { var r = await Client().DeleteAsync("rest/v1/" + table + query); await Ok(r); }
    public async Task<JsonElement> Rpc(string function, object body) { var r = await Client().PostAsJsonAsync("rest/v1/rpc/" + function, body, Json); await Ok(r); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone(); }
    public async Task<Guid> CreateAuthUser(AltaAdminRequest input) { var c = Client(true); var r = await c.PostAsJsonAsync("auth/v1/admin/users", new { email = input.Correo, password = input.Contrasena, email_confirm = true }, Json); await Ok(r); return Guid.Parse((await r.Content.ReadFromJsonAsync<JsonElement>(Json)).GetProperty("id").GetString()!); }
    public async Task<JsonElement> Login(LoginRequest input) { var c = Client(); var r = await c.PostAsJsonAsync("auth/v1/token?grant_type=password", new { email = input.Correo, password = input.Contrasena }, Json); await Ok(r); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone(); }
    public async Task<JsonElement> SignUp(RegistroRequest input, string emailRedirectTo)
    {
        var c = Client();
        var r = await c.PostAsJsonAsync("auth/v1/signup", new { email = input.Correo, password = input.Contrasena, email_redirect_to = emailRedirectTo }, Json);
        await Ok(r); return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement.Clone();
    }
    public async Task<bool> EmailExists(string email)
    {
        var r = await Client(true).GetAsync("auth/v1/admin/users?per_page=1000"); await Ok(r);
        using var result = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return result.RootElement.TryGetProperty("users", out var users) && users.EnumerateArray().Any(user => user.TryGetProperty("email", out var value) && string.Equals(value.GetString(), email, StringComparison.OrdinalIgnoreCase));
    }
    public async Task RecoverPassword(RecuperarContrasenaRequest input, string redirectTo) { var c = Client(); var r = await c.PostAsJsonAsync("auth/v1/recover", new { email = input.Correo, redirect_to = redirectTo }); await Ok(r); }
    public async Task UploadAvatar(Guid userId, Stream content, string? accessToken)
    {
        var c = string.IsNullOrWhiteSpace(accessToken) ? Client(true) : ClientForToken(accessToken);
        using var body = new StreamContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        var path = $"{userId}/avatar.jpg";
        var request = new HttpRequestMessage(HttpMethod.Put, "storage/v1/object/avatars/" + path) { Content = body };
        var r = await c.SendAsync(request);
        await Ok(r);
    }
    public string PublicAvatarUrl(Guid userId) => $"{_cfg.Url.TrimEnd('/')}/storage/v1/object/public/avatars/{userId}/avatar.jpg";
    private static async Task Ok(HttpResponseMessage r) { if (!r.IsSuccessStatusCode) throw new InvalidOperationException(await r.Content.ReadAsStringAsync()); }
}
