using System.ComponentModel.DataAnnotations;

namespace InternshipPortal.Web.Models;

/// <summary>Các vai trò hợp lệ của UserAccount.Role.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Company = "Company";
    public const string Student = "Student";
}

/// <summary>Tài khoản đăng nhập thật lưu trong CSDL (mật khẩu đã băm).</summary>
public class UserAccount
{
    public int Id { get; set; }

    /// <summary>Email (Admin/Company) hoặc Mã SV (Student).</summary>
    [Required]
    [StringLength(256)]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public string FullName { get; set; } = string.Empty;

    /// <summary>Chỉ nhận "Admin", "Company" hoặc "Student" (xem <see cref="Roles"/>).</summary>
    [Required]
    [StringLength(20)]
    public string Role { get; set; } = Roles.Student;

    /// <summary>CompanyId nếu Role = Company, StudentProfileId nếu Role = Student, null nếu Admin.</summary>
    public int? RelatedId { get; set; }
}
