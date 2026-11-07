# 🎓 InternshipPortal - Cổng Thông Tin Thực Tập Doanh Nghiệp

Dự án Web ASP.NET Core MVC chạy **.NET 8 (LTS)** kết nối **Entity Framework Core (SQL Server)** và thư viện xuất báo cáo **ClosedXML**.

---

## 🚀 Hướng Dẫn Chạy Dự Án Cho Các Thành Viên (Clone Về Máy Khác)

### 1. Yêu cầu hệ thống
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- SQL Server (Mặc định dùng **LocalDB** đi kèm Visual Studio).

### 2. Cấu hình CSDL (Database)
- Trong file `InternshipPortal.Web/appsettings.json`, connection string mặc định là:
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=InternshipDb;Trusted_Connection=True;MultipleActiveResultSets=true"
  }
  ```
- **Tự động tạo & Seed dữ liệu**: Khi chạy dự án lần đầu, `DbInitializer.Initialize()` sẽ tự động:
  1. Tạo CSDL `InternshipDb` trên LocalDB của máy bạn (nếu chưa có).
  2. Tạo các bảng dữ liệu: `Companies`, `JobPosts`, `StudentProfiles`, `Applications`.
  3. Tự động nạp sẵn dữ liệu mẫu: 2 công ty (**FPT Software**, **Viettel Solutions**) và 3 tin tuyển dụng thực tập.
- **Nếu máy bạn dùng SQL Server khác**:
  - Dùng SQL Express: Đổi thành `Server=.\\SQLEXPRESS;Database=InternshipDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`
  - Dùng SQL Server cục bộ: Đổi thành `Server=.;Database=InternshipDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true`

### 3. Lệnh khởi chạy ứng dụng
Mở terminal tại thư mục gốc của dự án và chạy:
```bash
dotnet run --project InternshipPortal.Web
```
Sau đó mở trình duyệt truy cập: `https://localhost:5001` hoặc `http://localhost:5000` (được định tuyến mặc định vào danh sách việc làm `Job/Index`).

---

## 🛠️ Nộp Bài / Tự Động Tạo Pull Request (`submit-work.bat`)
Khi bạn hoàn thành công việc và muốn đẩy code lên GitHub:
1. Nhấp đúp chuột chạy file `submit-work.bat` (hoặc gõ `.\submit-work.bat` trong terminal).
2. Script sẽ tự động:
   - Build dự án ở chế độ `Release` (ngăn chặn đẩy code nếu có lỗi biên dịch).
   - Tạo / chuyển sang nhánh `feature/day-1-views`.
   - Ủy quyền GitHub một lần duy nhất bằng **Device Code** (tự động copy mã và mở trình duyệt để bạn bấm xác nhận).
   - Đẩy code và **tự động tạo Pull Request vào `main` trên GitHub**.

---

## 📁 Cấu Trúc Dự Án
- `InternshipPortal.Web/Models/`: Chứa các thực thể `Company`, `JobPost`, `StudentProfile`, `Application`.
- `InternshipPortal.Web/Data/`: `ApplicationDbContext` và `DbInitializer`.
- `InternshipPortal.Web/Controllers/`: `JobController` (danh sách & chi tiết việc làm), `HomeController`.
- `InternshipPortal.Web/Views/`: Giao diện Razor Views Bootstrap 5.
