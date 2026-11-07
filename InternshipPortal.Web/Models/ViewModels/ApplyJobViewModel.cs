using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace InternshipPortal.Web.Models.ViewModels;

public class ApplyJobViewModel
{
    public int JobPostId { get; set; }

    public string JobTitle { get; set; } = string.Empty;

    public string CompanyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mã sinh viên")]
    [Display(Name = "Mã sinh viên")]
    public string StudentCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập họ và tên")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Chuyên ngành")]
    public string? Major { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập email")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
    [Display(Name = "Số điện thoại")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Display(Name = "Đính kèm CV (PDF, DOC, DOCX)")]
    public IFormFile? CVFile { get; set; }
}

