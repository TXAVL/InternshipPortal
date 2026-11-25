# 🎓 InternshipPortal - Cổng Thông Tin Thực Tập Doanh Nghiệp

Dự án Web ASP.NET Core MVC chạy **.NET 8 (LTS)** kết nối **Entity Framework Core (SQL Server / LocalDB)** và xuất báo cáo **ClosedXML**.

Tương thích hoàn hảo với **Visual Studio 2022** và **SQL Server 2022 / 2019 Express**.

---

## 🚀 Hướng Dẫn Chạy Dự Án (Visual Studio 2022)

### 1. Cấu hình CSDL (`appsettings.json`)
File cấu hình `appsettings.json` được đưa vào `.gitignore` để tránh xung đột cấu hình SQL giữa các thành viên.
- Nếu clone về máy mới chưa có file `appsettings.json`, bạn chỉ cần copy từ file mẫu [appsettings.example.json](file:///c:/Users/admin/Desktop/InternshipPortal/InternshipPortal.Web/appsettings.example.json):
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.\\SQLEXPRESS;Database=InternshipDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true",
  "LocalDbConnection": "Server=(localdb)\\mssqllocaldb;Database=InternshipDb;Trusted_Connection=True;MultipleActiveResultSets=true"
}
```

> **Lưu ý**: Cờ `TrustServerCertificate=True` giúp tránh lỗi mã hóa chứng chỉ SSL cục bộ trên các phiên bản SQL Server mới. Nếu máy bạn dùng **LocalDB**, chỉ cần đổi `DefaultConnection` trỏ sang chuỗi `LocalDbConnection`.

### 2. Khởi chạy và Tự Động Tạo CSDL
Khi bạn nhấn **F5** hoặc **Ctrl + F5** trong Visual Studio 2022 (hoặc gõ `dotnet run --project InternshipPortal.Web`):
- Hệ thống sẽ tự động gọi `DbInitializer.Initialize(context)`.
- Phương thức `context.Database.EnsureCreated()` sẽ **tự động tạo database `InternshipDb` trên SQL Server** (nếu chưa có).
- Tự động sinh toàn bộ 4 bảng: `Companies`, `JobPosts`, `StudentProfiles`, `Applications`.
- Tự động nạp sẵn dữ liệu mẫu:
  - 2 công ty mẫu: **FPT Software** và **Viettel Solutions**.
  - 3 tin tuyển dụng thực tập mẫu liên kết với 2 công ty.

---

## 🎨 Hướng Dẫn Dành Cho Bạn B (Phụ Trách Giao Diện HTML / CSS)

Bạn B clone project về để làm đẹp giao diện Ngày 1:
1. **Clone repo**: `git clone https://github.com/TXAVL/InternshipPortal.git`
2. **Mở solution**: Mở `InternshipPortal.sln` bằng Visual Studio 2022 và bấm **F5** để web chạy và tự sinh dữ liệu mẫu.
3. **Các file cần chỉnh sửa giao diện**:
   - `InternshipPortal.Web/Views/Shared/_Layout.cshtml`: Bố cục chung, Header, Navbar, Footer.
   - `InternshipPortal.Web/Views/Job/Index.cshtml`: Giao diện danh sách tin tuyển dụng, banner tìm kiếm, thẻ card việc làm.
   - `InternshipPortal.Web/Views/Job/Detail.cshtml`: Giao diện chi tiết việc làm, thông tin công ty, popup modal nộp đơn.
   - `InternshipPortal.Web/wwwroot/css/site.css`: Viết thêm CSS, chỉnh màu sắc, hiệu ứng nút bấm.
4. **Nộp bài**: Sau khi hoàn thành giao diện, chỉ cần nhấp đúp file **`submit-work-day2.bat`** ở thư mục gốc để tự động build và đẩy lên nhánh `feature/day-2-auth-ui` trên GitHub (kèm tạo Pull Request vào `main`)!

## Chạy test tự động

Bộ test (xUnit) chạy ứng dụng thật trên **một database tạm riêng**, tạo mới mỗi lần chạy và tự xóa khi xong. Test không đụng vào dữ liệu thật của `InternshipDb`, thư mục `uploads` hay cài đặt hệ thống.

```powershell
dotnet test InternshipPortal.Tests
```

- Cần có `InternshipPortal.Web/appsettings.json` (tạo từ `appsettings.example.json`). Test lấy chuỗi kết nối từ file này và chỉ đổi tên database. Muốn dùng máy chủ khác, đặt biến môi trường `TEST_SQL_CONNECTION`.
- Nội dung kiểm tra: phân quyền 3 vai trò và truy cập chéo giữa các công ty, đăng ký/đăng nhập, tin tuyển dụng, nộp hồ sơ và upload CV, nhật ký tuần, đánh giá và công bố điểm, xuất Excel, cài đặt hệ thống, khôi phục dữ liệu mẫu, cùng các test logic thuần.
- Phần JavaScript (TXAEditor, bộ lọc XSS, TXA UI, theme) chưa có test tự động, cần kiểm tra tay trên trình duyệt.
