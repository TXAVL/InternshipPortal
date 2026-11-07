# 🎓 InternshipPortal - Cổng Thông Tin Thực Tập Doanh Nghiệp

Dự án Web ASP.NET Core MVC chạy **.NET 8 (LTS)** kết nối **Entity Framework Core (SQL Server 2022 Express / LocalDB)** và xuất báo cáo **ClosedXML**.

Tương thích hoàn hảo với **Visual Studio 2022** và **SQL Server 2022 Express**.

---

## 🚀 Hướng Dẫn Chạy Trên Visual Studio 2022 & SQL Server 2022 Express

### 1. Môi trường phát triển
- **IDE**: Visual Studio 2022 (v17.8+ hỗ trợ .NET 8 LTS).
- **Hệ quản trị CSDL**: SQL Server 2022 Express (Instance name: `.\SQLEXPRESS`).

### 2. Cấu hình CSDL (`appsettings.json`)
Chuỗi kết nối mặc định trong `InternshipPortal.Web/appsettings.json` đã được cài sẵn cho **SQL Server 2022 Express**:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=InternshipDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
  "LocalDbConnection": "Server=(localdb)\\mssqllocaldb;Database=InternshipDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

> **Lưu ý quan trọng cho SQL Server 2022**: Cần có tham số `TrustServerCertificate=True` vì từ SQL Server 2022, Microsoft mặc định bật mã hóa kết nối (`Encrypt=True`), nếu không có cờ này sẽ phát sinh lỗi chứng chỉ SSL cục bộ.

### 3. Khởi chạy và Tự Động Tạo CSDL
Khi bạn nhấn **F5** hoặc **Ctrl + F5** trong Visual Studio 2022 (hoặc gõ `dotnet run --project InternshipPortal.Web`):
- Hệ thống sẽ tự động gọi `DbInitializer.Initialize(context)`.
- Phương thức `context.Database.EnsureCreated()` sẽ **tự động tạo database `InternshipDb` trên SQL Server 2022 Express** (nếu chưa có).
- Tự động sinh toàn bộ 4 bảng: `Companies`, `JobPosts`, `StudentProfiles`, `Applications`.
- Tự động nạp dữ liệu khởi tạo:
  - 2 công ty mẫu: **FPT Software** và **Viettel Solutions**.
  - 3 tin tuyển dụng thực tập mẫu liên kết với 2 công ty.

---

## 🛠️ Nộp Bài Tự Động (`submit-work.bat`)
Khi hoàn thành công việc:
1. Nhấp đúp vào file `submit-work.bat` ở thư mục gốc.
2. Script sẽ tự build Release, commit code và tự động ủy quyền GitHub Device Code để tạo Pull Request.
