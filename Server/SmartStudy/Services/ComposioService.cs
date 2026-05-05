using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SmartStudy.Services;

// HTTP client wrapper for the Composio.dev hosted-OAuth + tool-execution API.
public class ComposioService
{
    private const string BaseUrl = "https://backend.composio.dev/api/v3";
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    // Injects configuration and the HTTP client factory.
    public ComposioService(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    // True when the Composio integration is turned on in appsettings.
    public bool IsEnabled => _config.GetValue<bool>("Composio:Enabled");
    // The Composio API key from configuration.
    private string ApiKey => _config["Composio:ApiKey"] ?? "";
    // The Composio auth-config ID identifying this app's OAuth profile.
    private string AuthConfigId => _config["Composio:AuthConfigId"] ?? "";

    // Creates an HttpClient pre-loaded with the Composio API key header.
    private HttpClient CreateClient()
    {
        var client = _httpClientFactory.CreateClient("Composio");
        client.DefaultRequestHeaders.Add("x-api-key", ApiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    // Builds an absolute Composio API URL from a relative path.
    private static string Url(string path) => $"{BaseUrl}{path}";

    // Initiates a Google Calendar OAuth connection via Composio hosted auth.
    /// Returns the redirect URL the user should visit to authenticate.
    public async Task<ComposioConnectionResult> InitiateConnectionAsync(string userId, string callbackUrl)
    {
        using var client = CreateClient();

        var body = new
        {
            auth_config = new { id = AuthConfigId },
            connection = new
            {
                user_id = userId,
                callback_url = callbackUrl,
                state = new
                {
                    authScheme = "OAUTH2",
                    val = new { status = "INITIALIZING" }
                }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(Url("/connected_accounts"), content);
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new ComposioConnectionResult
            {
                Success = false,
                Message = $"Composio API error: {response.StatusCode} - {json}"
            };
        }

        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // Extract redirect URL from nested response
        string? redirectUrl = null;
        if (root.TryGetProperty("connection_data", out var connData) &&
            connData.TryGetProperty("val", out var val))
        {
            if (val.TryGetProperty("redirect_url", out var ru))
                redirectUrl = ru.GetString();
        }
        if (redirectUrl == null && root.TryGetProperty("redirect_url", out var ru2))
            redirectUrl = ru2.GetString();
        if (redirectUrl == null && root.TryGetProperty("redirectUrl", out var ru3))
            redirectUrl = ru3.GetString();

        var connId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;

        return new ComposioConnectionResult
        {
            Success = true,
            RedirectUrl = redirectUrl,
            ConnectedAccountId = connId
        };
    }

    // Lists connected accounts for a user, optionally filtered by status.
    public async Task<List<ComposioAccount>> GetConnectedAccountsAsync(string userId, string? status = null)
    {
        using var client = CreateClient();

        var url = $"/connected_accounts?user_ids={Uri.EscapeDataString(userId)}";
        if (!string.IsNullOrEmpty(status))
            url += $"&statuses={Uri.EscapeDataString(status)}";

        var response = await client.GetAsync(Url(url));
        if (!response.IsSuccessStatusCode)
            return new List<ComposioAccount>();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);

        var accounts = new List<ComposioAccount>();
        JsonElement items;

        if (doc.RootElement.TryGetProperty("items", out items))
        {
            foreach (var item in items.EnumerateArray())
            {
                accounts.Add(ParseAccount(item));
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                accounts.Add(ParseAccount(item));
            }
        }

        return accounts;
    }

    // Gets a specific connected account by ID.
    public async Task<ComposioAccount?> GetConnectedAccountAsync(string connectedAccountId)
    {
        using var client = CreateClient();
        var response = await client.GetAsync(Url($"/connected_accounts/{connectedAccountId}"));

        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return ParseAccount(doc.RootElement);
    }

    // Executes a Composio tool (e.g. GOOGLECALENDAR_LIST_EVENTS).
    public async Task<ComposioToolResult> ExecuteToolAsync(string toolSlug, string userId,
        string? connectedAccountId, Dictionary<string, object> arguments)
    {
        using var client = CreateClient();

        var body = new Dictionary<string, object>
        {
            ["arguments"] = arguments,
            ["entity_id"] = userId
        };

        if (!string.IsNullOrEmpty(connectedAccountId))
            body["connected_account_id"] = connectedAccountId;

        var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
        var response = await client.PostAsync(Url($"/tools/execute/{toolSlug}"), content);
        var json = await response.Content.ReadAsStringAsync();

        return new ComposioToolResult
        {
            Success = response.IsSuccessStatusCode,
            StatusCode = (int)response.StatusCode,
            RawJson = json
        };
    }

    // Deletes (disconnects) a connected account.
    public async Task<bool> DeleteConnectedAccountAsync(string connectedAccountId)
    {
        using var client = CreateClient();
        var response = await client.DeleteAsync(Url($"/connected_accounts/{connectedAccountId}"));
        return response.IsSuccessStatusCode;
    }

    // Parses a connected_account JSON element into a ComposioAccount.
    private static ComposioAccount ParseAccount(JsonElement el)
    {
        var account = new ComposioAccount
        {
            Id = el.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
            Status = el.TryGetProperty("status", out var st) ? st.GetString() ?? "" : ""
        };

        if (el.TryGetProperty("toolkit", out var toolkit) && toolkit.TryGetProperty("slug", out var slug))
            account.ToolkitSlug = slug.GetString() ?? "";

        if (el.TryGetProperty("createdAt", out var ca))
            account.CreatedAt = ca.GetString();
        else if (el.TryGetProperty("created_at", out var ca2))
            account.CreatedAt = ca2.GetString();

        return account;
    }
}

// Result of starting a Composio OAuth connection — carries the redirect URL.
public class ComposioConnectionResult
{
    public bool Success { get; set; }
    public string? RedirectUrl { get; set; }
    public string? ConnectedAccountId { get; set; }
    public string Message { get; set; } = "";
}

// Represents a single Composio connected account (e.g. one user's Google Calendar link).
public class ComposioAccount
{
    public string Id { get; set; } = "";
    public string Status { get; set; } = "";
    public string ToolkitSlug { get; set; } = "";
    public string? CreatedAt { get; set; }
}

// Raw response envelope from a Composio tool execution call.
public class ComposioToolResult
{
    public bool Success { get; set; }
    public int StatusCode { get; set; }
    public string RawJson { get; set; } = "";
}
