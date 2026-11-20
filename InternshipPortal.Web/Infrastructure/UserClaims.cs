using System.Security.Claims;

namespace InternshipPortal.Web.Infrastructure;

/// <summary>Tiện ích đọc thông tin người dùng từ Cookie Claims.</summary>
public static class UserClaims
{
    public const string RelatedIdType = "RelatedId";

    /// <summary>CompanyId (Company) hoặc StudentProfileId (Student) lưu trong claim "RelatedId".</summary>
    public static int? GetRelatedId(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst(RelatedIdType)?.Value;
        return int.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>Trả về CompanyId nếu người dùng là Company, ngược lại null (Admin xem toàn bộ).</summary>
    public static int? GetCompanyScope(this ClaimsPrincipal user) =>
        user.IsInRole(Models.Roles.Company) ? user.GetRelatedId() : null;
}
