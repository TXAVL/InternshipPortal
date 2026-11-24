using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using InternshipPortal.Web.Models;

namespace InternshipPortal.Web.Data;

public static class DbInitializer
{
    private const string TxaCompanyName = "TXA Studio";
    private const string DefaultPassword = "123456";

    public static void Initialize(ApplicationDbContext context)
    {
        context.Database.EnsureCreated();

        // DB tạo từ Ngày 1 đã tồn tại nên EnsureCreated() không thêm bảng mới -> tự bổ sung bảng Ngày 2
        EnsureNewTables(context);

        SeedCompaniesAndJobs(context);
        SeedTxaStudio(context);
        SeedAccounts(context);
        SeedEvaluation(context);
    }

    private static void SeedCompaniesAndJobs(ApplicationDbContext context)
    {
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

    private static void EnsureNewTables(ApplicationDbContext context)
    {
        context.Database.ExecuteSqlRaw(@"
IF OBJECT_ID(N'[dbo].[WeeklyReports]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[WeeklyReports] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_WeeklyReports] PRIMARY KEY,
        [ApplicationId] INT NOT NULL,
        [WeekNumber] INT NOT NULL,
        [TasksDone] NVARCHAR(MAX) NOT NULL,
        [NextWeekPlan] NVARCHAR(MAX) NOT NULL,
        [MentorFeedback] NVARCHAR(MAX) NULL,
        [CompanyFeedback] NVARCHAR(MAX) NULL,
        [SubmittedAt] DATETIME2 NOT NULL,
        CONSTRAINT [FK_WeeklyReports_Applications_ApplicationId] FOREIGN KEY ([ApplicationId])
            REFERENCES [dbo].[Applications] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_WeeklyReports_ApplicationId_WeekNumber]
        ON [dbo].[WeeklyReports] ([ApplicationId], [WeekNumber]);
END

IF OBJECT_ID(N'[dbo].[WeeklyReports]', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.WeeklyReports', N'CompanyFeedback') IS NULL
    ALTER TABLE [dbo].[WeeklyReports] ADD [CompanyFeedback] NVARCHAR(MAX) NULL;

IF OBJECT_ID(N'[dbo].[UserAccounts]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserAccounts] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_UserAccounts] PRIMARY KEY,
        [Username] NVARCHAR(256) NOT NULL,
        [PasswordHash] NVARCHAR(MAX) NOT NULL,
        [FullName] NVARCHAR(MAX) NOT NULL,
        [Role] NVARCHAR(20) NOT NULL,
        [RelatedId] INT NULL
    );
    CREATE UNIQUE INDEX [IX_UserAccounts_Username] ON [dbo].[UserAccounts] ([Username]);
END

IF OBJECT_ID(N'[dbo].[Evaluations]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[Evaluations] (
        [Id] INT IDENTITY(1,1) NOT NULL CONSTRAINT [PK_Evaluations] PRIMARY KEY,
        [ApplicationId] INT NOT NULL,
        [CompanyScore] FLOAT NULL,
        [CompanyComment] NVARCHAR(MAX) NULL,
        [SchoolScore] FLOAT NULL,
        [SchoolComment] NVARCHAR(MAX) NULL,
        [FinalScore] FLOAT NULL,
        [EvaluatedAt] DATETIME2 NULL,
        CONSTRAINT [FK_Evaluations_Applications_ApplicationId] FOREIGN KEY ([ApplicationId])
            REFERENCES [dbo].[Applications] ([Id]) ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX [IX_Evaluations_ApplicationId] ON [dbo].[Evaluations] ([ApplicationId]);
END");
    }

    /// <summary>Đánh giá mẫu cho sinh viên "test": Doanh nghiệp đã chấm, để trống điểm Khoa để demo chấm điểm.</summary>
    private static void SeedEvaluation(ApplicationDbContext context)
    {
        var application = context.Applications
            .Where(a => a.StudentProfile.StudentCode == "test" && a.Status == ApplicationStatus.Accepted)
            .OrderBy(a => a.Id)
            .FirstOrDefault();
        if (application == null || context.Evaluations.Any(e => e.ApplicationId == application.Id))
        {
            return;
        }

        context.Evaluations.Add(new Evaluation
        {
            ApplicationId = application.Id,
            CompanyScore = 8.5,
            CompanyComment = "Thái độ tốt, nắm bắt nhanh quy trình dự án.",
            EvaluatedAt = DateTime.Now
        });
        context.SaveChanges();
    }

    /// <summary>Công ty TXA Studio + tin tuyển dụng thực tập (idempotent, chạy được trên DB cũ).</summary>
    private static void SeedTxaStudio(ApplicationDbContext context)
    {
        var txa = context.Companies.FirstOrDefault(c => c.CompanyName == TxaCompanyName);
        if (txa == null)
        {
            txa = new Company
            {
                CompanyName = TxaCompanyName,
                Address = "Hà Nội",
                Website = "txastudio.click",
                ContactEmail = "hr@txastudio.click",
                IsApproved = true
            };
            context.Companies.Add(txa);
            context.SaveChanges();
        }

        if (!context.JobPosts.Any(j => j.CompanyId == txa.Id))
        {
            context.JobPosts.Add(new JobPost
            {
                CompanyId = txa.Id,
                Title = "Thực tập sinh Lập trình Web ASP.NET Core",
                Major = "CNTT",
                Location = "Hà Nội",
                Description = "Tham gia phát triển các dự án web nội bộ của TXA Studio bằng ASP.NET Core MVC, EF Core và SQL Server dưới sự hướng dẫn của mentor.",
                Requirements = "Sinh viên năm 3, 4 chuyên ngành CNTT. Nắm C#, OOP, SQL cơ bản. Chủ động, biết làm việc nhóm với Git.",
                SalaryRange = "3 - 5 triệu",
                Quantity = 3,
                ExpireDate = DateTime.Now.AddDays(60),
                IsActive = true
            });
            context.SaveChanges();
        }
    }

    /// <summary>
    /// 3 tài khoản thật (mật khẩu chung "123456", đã băm bằng PasswordHasher):
    /// admin@tdu.vn (Admin), hr@txastudio.click (Company), test (Student - đã Accepted + báo cáo Tuần 1).
    /// </summary>
    private static void SeedAccounts(ApplicationDbContext context)
    {
        var hasher = new PasswordHasher<UserAccount>();

        UserAccount NewAccount(string username, string fullName, string role, int? relatedId)
        {
            var account = new UserAccount { Username = username, FullName = fullName, Role = role, RelatedId = relatedId };
            account.PasswordHash = hasher.HashPassword(account, DefaultPassword);
            return account;
        }

        if (!context.UserAccounts.Any(u => u.Username == "admin@tdu.vn"))
        {
            context.UserAccounts.Add(NewAccount("admin@tdu.vn", "Ban Chủ Nhiệm Khoa", Roles.Admin, null));
        }

        var txa = context.Companies.FirstOrDefault(c => c.CompanyName == TxaCompanyName);
        if (txa != null && !context.UserAccounts.Any(u => u.Username == "hr@txastudio.click"))
        {
            context.UserAccounts.Add(NewAccount("hr@txastudio.click", "HR TXA Studio", Roles.Company, txa.Id));
        }

        context.SaveChanges();

        if (txa == null || context.UserAccounts.Any(u => u.Username == "test"))
        {
            return;
        }

        var student = context.StudentProfiles.FirstOrDefault(s => s.StudentCode == "test");
        if (student == null)
        {
            student = new StudentProfile
            {
                StudentCode = "test",
                FullName = "Sinh Viên Test",
                Major = "CNTT",
                Email = "test@tdu.vn",
                PhoneNumber = "0900000000"
            };
            context.StudentProfiles.Add(student);
            context.SaveChanges();
        }

        var job = context.JobPosts.Where(j => j.CompanyId == txa.Id).OrderBy(j => j.Id).First();
        var application = context.Applications
            .FirstOrDefault(a => a.StudentProfileId == student.Id && a.JobPostId == job.Id);
        if (application == null)
        {
            application = new Application
            {
                JobPostId = job.Id,
                StudentProfileId = student.Id,
                AppliedAt = DateTime.Now.AddDays(-14),
                Status = ApplicationStatus.Accepted
            };
            context.Applications.Add(application);
            context.SaveChanges();
        }

        if (!context.WeeklyReports.Any(r => r.ApplicationId == application.Id && r.WeekNumber == 1))
        {
            context.WeeklyReports.Add(new WeeklyReport
            {
                ApplicationId = application.Id,
                WeekNumber = 1,
                TasksDone = "Cài đặt môi trường Visual Studio 2022, SQL Server, làm quen quy trình Git của nhóm. "
                          + "Hoàn thành bài onboarding: xây dựng CRUD đơn giản với ASP.NET Core MVC và EF Core.",
                NextWeekPlan = "Nhận task đầu tiên trong sprint, tìm hiểu Dependency Injection và viết unit test.",
                MentorFeedback = "Em hoàn thành bài onboarding đúng hạn, chủ động hỏi khi vướng. "
                               + "Tuần tới chú ý đặt tên biến rõ ràng và viết commit message theo quy ước của nhóm.",
                CompanyFeedback = "Mentor tại TXA Studio: em làm quen quy trình nhanh, tuần sau bắt đầu nhận task thật trong sprint.",
                SubmittedAt = DateTime.Now.AddDays(-3)
            });
        }

        context.UserAccounts.Add(NewAccount("test", "Sinh Viên Test", Roles.Student, student.Id));
        context.SaveChanges();
    }
}
