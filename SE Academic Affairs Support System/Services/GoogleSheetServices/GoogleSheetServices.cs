// Services/GoogleSheetsService.cs
using System.Text;
using System.Text.Json;
using SE_Academic_Affairs_Support_System.Models;

public class GoogleSheetsService
{
    private readonly HttpClient _http;
    private readonly string _scriptUrl;

    public GoogleSheetsService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _scriptUrl = config["GoogleAppsScript:Url"]!;
    }

    public async Task<List<TopicSheet>> GetTopicsAsync(string sheetId)
    {
        var url = $"{_scriptUrl}?action=topics&sheetId={Uri.EscapeDataString(sheetId)}";
        var res = await _http.GetAsync(url);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<TopicSheet>>(json, Options()) ?? [];
    }

    public async Task<ApiResponse> RegisterAsync(RegisterTopicRequest req)
    {
        var body = new StringContent(
            JsonSerializer.Serialize(req, Options()),
            Encoding.UTF8, "application/json");
        var res = await _http.PostAsync(_scriptUrl, body);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse>(json, Options())
               ?? new ApiResponse { Success = false, Message = "Lỗi không xác định" };
    }

    private static JsonSerializerOptions Options() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}