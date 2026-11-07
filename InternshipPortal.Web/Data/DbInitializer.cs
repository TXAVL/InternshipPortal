using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Data;

public static class DbInitializer
{
    public static void Initialize(ApplicationDbContext context)
    {
        context.Database.EnsureCreated();

        if (context.Companies.Any())
        {
            return;
        }

        var fpt = new Company
        {
            CompanyName = "FPT Software",
            Address = "Khu Công nghệ cao Hòa Lạc, Thạch Thất, Hà Nội",
            Website = "https://fptsoftware.com",
            ContactEmail = "recruitment@fpt.com",
            IsApproved = true
        };

        var viettel = new Company
        {
            CompanyName = "Viettel Solutions",
            Address = "Số 1 Trần Hữu Dực, Mỹ Đình, Nam Từ Liêm, Hà Nội",
            Website = "https://viettelsolutions.vn",
            ContactEmail = "tuyendung@viettelsolutions.vn",
            IsApproved = true
        };

        context.Companies.AddRange(fpt, viettel);

        var jobPosts = new List<JobPost>
        {
            new()
            {
                Company = fpt,
                Title = "Thực tập sinh .NET / C# Backend",
                Major = "CNTT",
                Location = "Hà Nội",
                Description = "Tham gia nghiên cứu, phát triển và bảo trì các hệ thống phần mềm doanh nghiệp trên nền tảng .NET 8, ASP.NET Core MVC và Entity Framework Core.",
                Requirements = "Sinh viên năm 3, 4 hoặc mới tốt nghiệp chuyên ngành CNTT. Có kiến thức cơ bản về C#, OOP, SQL Server. Ham học hỏi và chủ động trong công việc.",
                SalaryRange = "3 - 5 triệu",
                Quantity = 5,
                ExpireDate = DateTime.Now.AddDays(30),
                IsActive = true
            },
            new()
            {
                Company = fpt,
                Title = "Thực tập sinh Lập trình Web Frontend (React / TypeScript)",
                Major = "CNTT",
                Location = "Hà Nội",
                Description = "Xây dựng các giao diện người dùng hiện đại, tích hợp RESTful API cho các giải pháp chuyển đổi số của khách hàng toàn cầu.",
                Requirements = "Nắm vững HTML, CSS, JavaScript/TypeScript. Có hiểu biết về React hoặc framework tương đương. Có tư duy thẩm mỹ và UI/UX tốt.",
                SalaryRange = "3 - 5 triệu",
                Quantity = 3,
                ExpireDate = DateTime.Now.AddDays(30),
                IsActive = true
            },
            new()
            {
                Company = viettel,
                Title = "Thực tập sinh Kỹ sư Dữ liệu (Data Engineer)",
                Major = "CNTT",
                Location = "Hà Nội",
                Description = "Hỗ trợ xây dựng, vận hành và tối ưu hóa các pipeline xử lý dữ liệu lớn (ETL), phục vụ hệ thống báo cáo và phân tích thông minh.",
                Requirements = "Nền tảng tốt về Python, SQL và cấu trúc dữ liệu - giải thuật. Hiểu biết căn bản về Database quan hệ và NoSQL là lợi thế.",
                SalaryRange = "4 - 6 triệu",
                Quantity = 2,
                ExpireDate = DateTime.Now.AddDays(30),
                IsActive = true
            }
        };

        context.JobPosts.AddRange(jobPosts);
        context.SaveChanges();
    }
}

