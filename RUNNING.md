# Chạy đồ án trên Windows với SQL Server LocalDB

## Luồng chính — cập nhật 17/11/2026

Luồng mô phỏng web **không cần Python Agent, enrollment token hoặc quyền riêng từng máy**. API mặc định `Simulation:UseAgent=false`. Admin tạo/kích hoạt Employee và thêm máy Desktop/Laptop/Server ở trạng thái Sẵn sàng hoặc Đã bàn giao. Mọi Employee active được đăng nhập các máy này.

1. Admin vào `/admin` → Tài khoản để tạo Employee; Tài sản để thêm máy, mở **Cấu hình mô phỏng** nhập thông số/phần mềm. Dữ liệu chưa nhập hiển thị “Chưa khai báo”.
2. Employee mở `/` hoặc `/employee` → chọn máy → đăng nhập → xem Chi tiết, Phần mềm, Desktop/Documents. Không có quyền sửa cấu hình máy.
3. Trong Desktop/Documents: tạo/sửa TXT/CSV, nhập Office/PDF, đổi tên/di chuyển/sao chép/xóa/khôi phục. Mặc định 10 file/profile; quản trị chỉnh ở Quota.

File lưu thật trên server: mặc định `backend/src/ITManagement.API/storage/simulation` khi chạy API từ project này; có thể đặt `Simulation__StorageRoot` thành đường dẫn tuyệt đối riêng. Blob dùng GUID, tách theo máy/người/profile; tên và thư mục Desktop/Documents/RecycleBin lưu trong SQL. Không dùng đường dẫn phần mềm trong bảng Software để mở file trên server. Nội dung nằm ngoài web root, chỉ đọc qua phiên hợp lệ. Phiên bản blob cũ/orphan được giữ, chưa có tác vụ tự dọn; cần sao lưu cả DB và kho file.

Migration mới: `20261116145149_ServerSimulation` chỉ thêm cột/bảng. Sao lưu DB trước khi áp dụng; không xóa/reseed dữ liệu. Profile Agent cũ được đánh dấu `Agent` và không tự đổi sang `Server`. Người dùng chọn giữ file cũ, chuyển sau; profile Agent cũ và profile Server mới được giữ riêng cho cùng người–máy, nên dữ liệu cũ không chặn đăng nhập mới. File Agent cũ chưa xuất hiện trong Desktop/Documents mới cho đến khi chuyển nội dung. Migration SeparateServerAndAgentProfiles đổi khóa duy nhất theo người–máy–backend; không downgrade khi đã có cả hai profile nếu chưa có kế hoạch gộp dữ liệu.

Kiểm thử SQL + trình duyệt không Agent (DB GUID riêng, cổng5189/5190, tự cleanup):

```powershell
dotnet build backend/ITManagement.slnx --artifacts-path artifacts/server-simulation
# Đặt PLAYWRIGHT_MODULE nếu Playwright cài bên ngoài workspace.
.\.test-venv\Scripts\python.exe backend\tests\server_simulation_e2e.py
```

Python ở lệnh trên chỉ điều phối kiểm thử; không cần Python để chạy web/API hằng ngày. Các hướng dẫn Agent bên dưới là luồng cũ, chỉ dùng khi chủ động đặt `Simulation__UseAgent=true`; không dùng cho demo mô phỏng web mới.

## Môi trường
- Chạy ứng dụng: .NET SDK hỗ trợ net10.0, Node.js/npm. Python3.11+ chỉ cần cho bộ kiểm thử hoặc Agent legacy.
- SQL Server Express LocalDB: `(localdb)\MSSQLLocalDB`, Windows Authentication.
- Không dùng Docker. Khi alias LocalDB không tự kết nối trong môi trường hiện tại, mở SSMS kết nối `(localdb)\MSSQLLocalDB`; không reset instance.
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

Mở http://localhost:5173. URL gốc tự chuyển đến `/employee`; Vite chuyển `/api` sang API cổng 5038.
- `/employee`: chọn máy trước, sau đó đăng nhập tài khoản nhân viên.
- `/admin`: nhập trực tiếp URL để mở cổng Admin, nền navy và thẻ đăng nhập trắng. Không có liên kết sang Admin ở cổng nhân viên/trang chọn máy. Chỉ tài khoản Admin đăng nhập tại cổng này.
- `/login`: đường dẫn đăng nhập tương thích; tài khoản sẽ được đưa đến khu vực theo vai trò.
- `/machines`: chọn máy, đăng nhập Domain ID mô phỏng và làm việc với profile.

Giao diện nhân viên nằm chung React SPA trong `frontend/admin`; `frontend/employee` chưa có ứng dụng riêng.
`?demo=1` là dữ liệu mô phỏng trong trình duyệt; dùng `?demo=0` để trở lại API thật. Không dùng kết quả demo làm bằng chứng lưu file thật trên server.

## Python Agent — legacy, tùy chọn

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

## Tài liệu và luồng cũ

- [Lịch sử nghiệm thu và demo Agent](legacy/README.md): giữ để tra cứu, không phải hướng dẫn demo hiện tại.
- [Python Agent](agent/README.md): phần mở rộng cũ, không cần chạy cho mô phỏng web.
- Storage cũ theo quota byte vẫn được giữ cùng dữ liệu/API. Luồng mới dùng profile theo người–máy và quota số file; chưa hợp nhất hay xóa storage cũ.
- File Agent cũ được giữ nguyên theo lựa chọn của người dùng; chỉ chuyển khi có đúng thư mục nguồn.
