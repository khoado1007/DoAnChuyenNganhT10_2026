# Chạy đồ án trên Windows với SQL Server LocalDB

## Môi trường
- .NET SDK hỗ trợ net10.0, Node.js/npm và Python 3.11+.
- SQL Server Express LocalDB: `(localdb)\MSSQLLocalDB`, Windows Authentication.
- Không dùng Docker. Không cần mở SSMS để chạy API; LocalDB tự khởi động khi kết nối.
- Chạy lệnh từ thư mục gốc repository. Không đổi/xóa database `ITManagement` để sửa lỗi test.

## API và database hiện có

```powershell
dotnet restore backend/ITManagement.slnx
dotnet run --project backend/src/ITManagement.API --launch-profile http
```

API: http://localhost:5038; Swagger Development: http://localhost:5038/swagger.
Mặc định API không tự migrate/seed. Database hiện có được giữ nguyên.

Chỉ khi cần khởi tạo database mới hoặc đã kiểm tra migration và backup, bật `Database:InitializeOnStartup` một lần và cấu hình `SeedAdmin:Password` bằng mật khẩu riêng. Trong PowerShell có thể nhập ẩn:

```powershell
$env:SeedAdmin__Password = [System.Net.NetworkCredential]::new('', (Read-Host 'Mật khẩu admin mới' -AsSecureString)).Password
$env:Database__InitializeOnStartup = 'true'
try {
    dotnet run --project backend/src/ITManagement.API --launch-profile http
} finally {
    Remove-Item Env:SeedAdmin__Password -ErrorAction SilentlyContinue
    Remove-Item Env:Database__InitializeOnStartup -ErrorAction SilentlyContinue
}
```

Seeder giữ tài khoản admin hiện có, không đặt lại mật khẩu. Không chạy quy trình khởi tạo trên database dùng chung khi chưa xem migration.
Các migration SQL Server nằm trong `Persistence/SqlServerMigrations`; không xóa migration đã áp dụng.

## Frontend

Trong terminal thứ hai:

```powershell
npm --prefix frontend/admin ci
npm --prefix frontend/admin run dev
```

Mở http://localhost:5173. Vite chuyển `/api` sang API cổng 5038.
- `/login`: đăng nhập tài khoản ứng dụng.
- `/admin`: quản trị.
- `/employee`: cổng nhân viên.
- `/machines`: chọn máy, đăng nhập Domain ID mô phỏng và làm việc với profile.

Giao diện nhân viên nằm chung React SPA trong `frontend/admin`; `frontend/employee` chưa có ứng dụng riêng.
`?demo=1` là dữ liệu mô phỏng trong trình duyệt; dùng `?demo=0` để trở lại API thật. Không dùng kết quả demo làm bằng chứng lưu file qua Agent.

## Python Agent

Xem [agent/README.md](agent/README.md). Chạy `serve-files` với state và root riêng theo Real/Simulated. Enrollment không tạo tài khoản nhân viên. Không dùng Desktop/Documents thật của Windows làm root.

## Kiểm thử

```powershell
dotnet test backend/ITManagement.slnx
npm --prefix frontend/admin test
npm --prefix frontend/admin run build
.\.test-venv\Scripts\python.exe -m pytest agent/tests -q
```

Nếu chưa có Python test environment:

```powershell
python -m venv .test-venv
.\.test-venv\Scripts\python.exe -m pip install -r agent/requirements-dev.txt
```

Integration tests tạo database `itmanagement_test_<GUID>` trên LocalDB, migrate/seed riêng và xóa database test khi hoàn tất. Không dùng `ITManagement`. Có thể đặt `TEST_SQLSERVER` để chọn SQL Server test khác; tên database trong chuỗi kết nối luôn được thay bằng tên test mới. Dashboard test dùng cùng cấu hình server và tiền tố `itam_dashboard_test_`.
Nếu LocalDB lỗi khởi động, kiểm tra kết nối SSMS và `sqllocaldb info MSSQLLocalDB`; không xóa instance hoặc dữ liệu để thử sửa.

Các file `backend/docker-compose.yml`, Dockerfile và test reverse-proxy cổng 8088 là tài liệu/công cụ cũ; không cần chạy trong cấu hình Windows này. Chạy test giao diện thật bằng các smoke script chỉ với database demo/test, vì chúng tạo dữ liệu.

## Kết quả xác minh ngày 11/11/2026

- Backend: 82/82 test đạt trên SQL Server LocalDB, bao gồm migration từ database mới, snapshot khớp model, migrate lại giữ dữ liệu, cấp phát đồng thời, token Agent, phiên thiết bị và quota.
- Lệnh: `dotnet test backend/ITManagement.slnx --no-restore --logger "console;verbosity=normal"`.
- Trong tiến trình Codex, phân giải alias LocalDB báo lỗi dù SQL Server đang chạy. Lần kiểm tra dùng `TEST_SQLSERVER` trỏ tới named pipe hiện tại của chính instance đó, với Windows Authentication; không thay đổi cấu hình SQL Server. Địa chỉ pipe thay đổi khi instance khởi động lại, không lưu nó vào appsettings.
- Chỉ database test có GUID được tạo/xóa; không migrate database `ITManagement`.
- Phạm vi lần này là chuyển đổi backend/database. Lỗi Python Agent và nghiệm thu toàn bộ giao diện với Agent thật chưa được xử lý trong thay đổi này.
