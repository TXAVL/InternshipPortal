namespace InternshipPortal.Web.Models;

/// <summary>
/// Đánh giá và chấm điểm cuối kỳ cho một đơn thực tập đã Trúng tuyển (quan hệ 1-1 với Application).
/// Doanh nghiệp chấm 40%, Khoa / GVHD chấm 60%.
/// </summary>
public class Evaluation
{
    public const double CompanyWeight = 0.4;
    public const double SchoolWeight = 0.6;

    public int Id { get; set; }

    public int ApplicationId { get; set; }

    /// <summary>Điểm Doanh nghiệp chấm (thang 10).</summary>
    public double? CompanyScore { get; set; }

    public string? CompanyComment { get; set; }

    /// <summary>Điểm Ban chủ nhiệm Khoa / GVHD chấm (thang 10).</summary>
    public double? SchoolScore { get; set; }

    public string? SchoolComment { get; set; }

    /// <summary>Điểm tổng kết, chỉ có khi đã đủ cả hai đầu điểm.</summary>
    public double? FinalScore { get; set; }

    public DateTime? EvaluatedAt { get; set; }

    /// <summary>Sinh viên chỉ thấy kết quả khi Khoa đã công bố điểm.</summary>
    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Application Application { get; set; } = null!;

    /// <summary>Tính lại điểm tổng kết: 40% Doanh nghiệp + 60% Khoa; thiếu một đầu điểm thì để null.</summary>
    public void Recalculate()
    {
        FinalScore = CompanyScore.HasValue && SchoolScore.HasValue
            ? Math.Round(CompanyScore.Value * CompanyWeight + SchoolScore.Value * SchoolWeight, 2)
            : null;
    }
}
