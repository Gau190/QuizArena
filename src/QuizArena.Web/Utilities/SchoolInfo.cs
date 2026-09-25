using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QuizArena.Infrastructure.Data;

namespace QuizArena.Web.Utilities;

/// <summary>Tên đơn vị lấy từ Cài đặt tổ chức (thay cho tên trường gắn cứng trong giao diện).</summary>
public class SchoolInfo(QuizArenaDbContext db, IMemoryCache cache)
{
    public async Task<string> NameAsync()
    {
        if (cache.TryGetValue("school_name", out string? cached) && cached is not null) return cached;
        var name = await db.Organizations.AsNoTracking().OrderBy(x => x.Id).Select(x => x.Name).FirstOrDefaultAsync();
        name = string.IsNullOrWhiteSpace(name) ? "Chưa cập nhật tên đơn vị" : name;
        cache.Set("school_name", name, TimeSpan.FromMinutes(5));
        return name;
    }
}
