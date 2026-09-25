namespace QuizArena.Web.Utilities;

/// <summary>Giới hạn khi nhập tệp: loại tệp, dung lượng, số dòng; kiểm tra chữ ký ảnh cho logo.</summary>
public static class UploadRules
{
    public const long MaxImportBytes = 2 * 1024 * 1024;
    public const int MaxImportRows = 2000;

    public static bool IsAllowedImport(IFormFile file, string[] extensions, out string error)
    {
        error = "";
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!extensions.Contains(ext))
        {
            error = $"Chỉ nhận tệp {string.Join(" hoặc ", extensions)}.";
            return false;
        }

        if (file.Length > MaxImportBytes)
        {
            error = "Tệp quá lớn (tối đa 2 MB).";
            return false;
        }

        return true;
    }

    /// <summary>Đọc vài byte đầu để chắc chắn đúng là ảnh PNG/JPEG/WEBP (không chỉ tin phần mở rộng).</summary>
    public static async Task<bool> LooksLikeImageAsync(IFormFile file)
    {
        var head = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(head.AsMemory(0, 12));
        if (read < 12) return false;
        var png = head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47;
        var jpg = head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF;
        var webp = head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P';
        return png || jpg || webp;
    }

    /// <summary>Vô hiệu công thức Excel: ô bắt đầu bằng = + - @ được thêm dấu nháy đơn phía trước.</summary>
    public static string SafeCell(string? value)
    {
        var text = value ?? "";
        return text.Length > 0 && "=+-@\t\r".Contains(text[0]) ? "'" + text : text;
    }
}
