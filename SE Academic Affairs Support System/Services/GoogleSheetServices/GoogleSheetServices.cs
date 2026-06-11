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

    // GoogleSheetsService.cs — RegisterAsync
    public async Task<ApiResponse> RegisterAsync(RegisterTopicRequest req)
    {
        var payload = new
        {
            action = "register",
            sheetId = req.SheetId,
            rowIndex = req.RowIndex,
            studentId = req.StudentId,
            studentName = req.StudentName,
            studentId2 = req.StudentId2,
            studentName2 = req.StudentName2
        };

        var json = JsonSerializer.Serialize(payload, Options());

        // ── Log xem gửi gì ────────────────────────────────────────────
        Console.WriteLine(">>> Payload gửi AppScript: " + json);

        var body = new StringContent(json, Encoding.UTF8, "application/json");
        var res = await _http.PostAsync(_scriptUrl, body);

        // ── Log response thô ──────────────────────────────────────────
        var responseText = await res.Content.ReadAsStringAsync();
        Console.WriteLine(">>> AppScript trả về: " + responseText);

        return JsonSerializer.Deserialize<ApiResponse>(responseText, Options())
               ?? new ApiResponse { Success = false, Message = "Lỗi không xác định" };
    }
    private static JsonSerializerOptions Options() => new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    public async Task<List<GradingSheetRow>> GetGradingRowsAsync(string sheetId)
    {
        var url = $"{_scriptUrl}?action=grading&sheetId={Uri.EscapeDataString(sheetId)}";
        var res = await _http.GetAsync(url);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        Console.WriteLine(json);
        return JsonSerializer.Deserialize<List<GradingSheetRow>>(json, Options()) ?? [];
    }

    // ─── 2. Lưu / cập nhật điểm ──────────────────────────────────────────
    /// <summary>
    /// Gọi Apps Script để ghi điểm vào sheet "BangDiem".
    /// Nếu MSSV đã tồn tại → cập nhật; chưa có → thêm dòng mới.
    /// </summary>
    public async Task<ApiResponse> GradeAsync(GradeTopicRequest req)
    {
        var payload = new
        {
            action = "grade",
            sheetId = req.SheetId,
            mssv = req.Mssv,
            score = req.Score,
            gradedBy = req.GradedBy,
            gradedAt = DateTime.Now.ToString("dd/MM/yyyy HH:mm")
        };

        var body = new StringContent(
            JsonSerializer.Serialize(payload, Options()),
            System.Text.Encoding.UTF8, "application/json");

        var res = await _http.PostAsync(_scriptUrl, body);
        res.EnsureSuccessStatusCode();
        var json = await res.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<ApiResponse>(json, Options())
               ?? new ApiResponse { Success = false, Message = "Lỗi không xác định" };
    }

}