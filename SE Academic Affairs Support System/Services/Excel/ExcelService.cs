using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;

namespace SE_Academic_Affairs_Support_System.Services.Excel
{
    public class ExcelService : IExcelService
    {
        private const string DateTimeFormat = "dd/MM/yyyy HH:mm";

        public string? ValidateUploadedFile(IFormFile? file, long maxBytes = 5 * 1024 * 1024)
        {
            if (file == null || file.Length == 0)
                return "Vui lòng chọn một file để tải lên.";

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext != ".xlsx")
                return "Định dạng không hợp lệ. Vui lòng tải lên file Excel (.xlsx).";

            if (file.Length > maxBytes)
                return $"File quá lớn (tối đa {maxBytes / (1024 * 1024)} MB). Vui lòng chia nhỏ danh sách.";

            return null;
        }

        public List<ExcelRow> ReadRows(IFormFile file, int columnCount)
        {
            var rows = new List<ExcelRow>();
            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var ws = workbook.Worksheets.FirstOrDefault()
                         ?? throw new ExcelReadException("File Excel không có worksheet nào.");

                int lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
                for (int r = 2; r <= lastRow; r++)
                {
                    var row = ws.Row(r);
                    if (row.IsEmpty()) continue;

                    var cells = new string[columnCount];
                    for (int c = 0; c < columnCount; c++)
                        cells[c] = row.Cell(c + 1).GetString().Trim();

                    // Bỏ qua dòng mà tất cả các ô đều rỗng
                    if (cells.All(string.IsNullOrEmpty)) continue;

                    rows.Add(new ExcelRow(r, cells));
                }
            }
            catch (ExcelReadException)
            {
                throw;
            }
            catch (Exception)
            {
                throw new ExcelReadException("Không thể đọc file Excel. File có thể bị hỏng hoặc không đúng định dạng .xlsx.");
            }

            return rows;
        }

        public byte[] BuildTemplate(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<string>? sampleRow = null)
        {
            var rows = sampleRow != null
                ? new List<IReadOnlyList<object?>> { sampleRow.Cast<object?>().ToList() }
                : Enumerable.Empty<IReadOnlyList<object?>>();
            return BuildWorkbook(sheetName, headers, rows);
        }

        public byte[] BuildWorkbook(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(string.IsNullOrWhiteSpace(sheetName) ? "Sheet1" : sheetName);

            // Header
            for (int c = 0; c < headers.Count; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e3a8a");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data
            int rowIdx = 2;
            foreach (var row in rows)
            {
                for (int c = 0; c < row.Count; c++)
                {
                    var cell = ws.Cell(rowIdx, c + 1);
                    SetCellValue(cell, row[c]);
                }
                rowIdx++;
            }

            ws.Columns().AdjustToContents();
            // Giới hạn độ rộng để cột mô tả dài không phá layout
            foreach (var col in ws.ColumnsUsed())
                if (col.Width > 60) col.Width = 60;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        }

        private static void SetCellValue(IXLCell cell, object? value)
        {
            switch (value)
            {
                case null:
                    cell.Value = string.Empty;
                    break;
                case DateTime dt:
                    cell.Value = dt;
                    cell.Style.DateFormat.Format = DateTimeFormat;
                    break;
                case int i:
                    cell.Value = i;
                    break;
                case double d:
                    cell.Value = d;
                    break;
                case decimal m:
                    cell.Value = m;
                    break;
                default:
                    cell.Value = value.ToString() ?? string.Empty;
                    break;
            }
        }
    }
}
