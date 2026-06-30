using Microsoft.AspNetCore.Http;

namespace SE_Academic_Affairs_Support_System.Services.Excel
{
    /// <summary>
    /// Lớp dùng chung để đọc/ghi file Excel (.xlsx) bằng ClosedXML.
    /// Tách riêng phần I/O Excel để các tính năng Import/Export tái sử dụng,
    /// không lặp lại logic mở workbook / build file ở nhiều nơi.
    /// </summary>
    public interface IExcelService
    {
        /// <summary>
        /// Kiểm tra file upload hợp lệ (không rỗng, đúng .xlsx, không quá lớn).
        /// Trả về thông báo lỗi thân thiện nếu không hợp lệ, hoặc null nếu hợp lệ.
        /// </summary>
        string? ValidateUploadedFile(IFormFile? file, long maxBytes = 5 * 1024 * 1024);

        /// <summary>
        /// Đọc các dòng dữ liệu từ worksheet đầu tiên (bỏ qua dòng tiêu đề ở dòng 1).
        /// Mỗi dòng trả về mảng chuỗi đã trim với độ dài cố định = <paramref name="columnCount"/>.
        /// Các dòng trống hoàn toàn bị bỏ qua. Số dòng Excel gốc đi kèm để báo lỗi.
        /// Ném <see cref="ExcelReadException"/> với thông báo thân thiện nếu file hỏng / sai định dạng.
        /// </summary>
        List<ExcelRow> ReadRows(IFormFile file, int columnCount);

        /// <summary>Sinh file mẫu chỉ gồm dòng tiêu đề (+ dòng ví dụ tùy chọn).</summary>
        byte[] BuildTemplate(string sheetName, IReadOnlyList<string> headers, IReadOnlyList<string>? sampleRow = null);

        /// <summary>Sinh file Excel từ tiêu đề + danh sách dòng (mỗi ô là object: chuỗi/số/DateTime...).</summary>
        byte[] BuildWorkbook(string sheetName, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<object?>> rows);
    }

    /// <summary>Một dòng đã đọc từ Excel kèm số dòng gốc (1-based) để báo lỗi.</summary>
    public sealed record ExcelRow(int RowNumber, string[] Cells)
    {
        public string Get(int index) => index >= 0 && index < Cells.Length ? Cells[index] : string.Empty;
    }

    /// <summary>Lỗi đọc file Excel với thông báo đã thân thiện cho người dùng.</summary>
    public sealed class ExcelReadException(string message) : Exception(message);
}
