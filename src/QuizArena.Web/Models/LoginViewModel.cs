using System.ComponentModel.DataAnnotations;

namespace QuizArena.Web.Models;

public class LoginViewModel
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [StringLength(50)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
    [StringLength(100)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
