using Microsoft.AspNetCore.Http;

namespace SE_Academic_Affairs_Support_System.ViewModels
{
    // ── Admin: Import Devices from Excel ──────────────────────────────────────
    public class ImportDevicesViewModel
    {
        public IFormFile? File { get; set; }

        // Kết quả sau khi xử lý (POST)
        public bool IsProcessed { get; set; }
        public int Created { get; set; }
        public int Skipped { get; set; }
        public List<string> Errors { get; set; } = new();
    }
}
