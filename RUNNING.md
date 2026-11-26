# Chạy đồ án trên Windows với SQL Server LocalDB

## Luồng chính — cập nhật 26/11/2026

Luồng mô phỏng web **không cần Python Agent, enrollment token hoặc quyền riêng từng máy**. API mặc định `Simulation:UseAgent=false`. Admin tạo/kích hoạt Employee và thêm máy Desktop/Laptop/Server ở trạng thái Sẵn sàng hoặc Đã bàn giao. Mọi Employee active được đăng nhập các máy này.

1. Admin vào `/admin` → Tài khoản để tạo Employee; Tài sản để thêm máy, mở **Cấu hình mô phỏng** nhập thông số/phần mềm. Dữ liệu chưa nhập hiển thị “Chưa khai báo”.
2. Employee mở `/` hoặc `/employee` → chọn máy → xem tên, mã máy → đăng nhập một lần → mở ngay Desktop/Documents. Chi tiết máy và Phần mềm nằm cùng màn hình. Không có quyền sửa cấu hình máy.
3. Trong **Desktop/Documents**, Employee làm việc với file của mình trên máy đã chọn. Hạn mức mặc định là 10 file trên tất cả máy. Admin có thể chỉnh trong **Quota tài khoản**.
4. Mở **Chi tiết** để xem danh sách và lịch sử thay đổi file của những người đã dùng máy này. Mở **Phần mềm** để xem ứng dụng và dịch vụ do Admin khai báo.

File lưu thật trên server: mặc định `backend/src/ITManagement.API/storage/simulation` khi chạy API từ project này; có thể đặt `Simulation__StorageRoot` thành đường dẫn tuyệt đối riêng. Blob dùng GUID, tách theo máy/người/profile; tên và thư mục Desktop/Documents/RecycleBin lưu trong SQL. Không dùng đường dẫn phần mềm trong bảng Software để mở file trên server. Nội dung nằm ngoài web root, chỉ đọc qua phiên hợp lệ. Phiên bản blob cũ/orphan được giữ, chưa có tác vụ tự dọn; cần sao lưu cả DB và kho file.

Migration `ServerSimulation` bổ sung kho server và cấu hình máy. `SeparateServerAndAgentProfiles` tạo hồ sơ riêng theo người–máy–backend. Mỗi người dùng mở hồ sơ Server của mình trong luồng web hiện tại. Các bản ghi Agent cũ được giữ để tra cứu lịch sử. Hạng mục chuyển một file Agent cũ đã được chủ đồ án khép lại theo quyết định bỏ qua ngày 26/11/2026.

Kiểm thử SQL + trình duyệt không Agent (DB GUID riêng, cổng5189/5190, tự cleanup):

```powershell
dotnet build backend/ITManagement.slnx --artifacts-path artifacts/server-simulation
# Đặt PLAYWRIGHT_MODULE nếu Playwright cài bên ngoài workspace.
.\.test-venv\Scripts\python.exe backend\tests\server_simulation_e2e.py
```

Python ở lệnh trên chỉ điều phối kiểm thử; không cần Python để chạy web/API hằng ngày. Các hướng dẫn Agent bên dưới là luồng cũ, chỉ dùng khi chủ động đặt `Simulation__UseAgent=true`; không dùng cho demo mô phỏng web mới.

Đăng xuất trả về màn hình đăng nhập của cùng máy. Bấm **Chọn máy khác** để đổi thiết bị. Khi tab báo rời trang, máy chủ chờ 60 giây. F5 hoặc quay lại trong thời gian này sẽ khôi phục phiên còn hợp lệ. Nếu trình duyệt bị buộc tắt hoặc mất mạng trước khi gửi tín hiệu, phiên máy hết hạn theo hạn gốc (tối đa 8 giờ). Để tab mở ở nền không kích hoạt đếm ngược.

Đối với database hiện có, sao lưu rồi cập nhật đến migration `AccountFileQuota` trước khi chạy API mới. Với tài khoản từng có nhiều hạn mức khác nhau, migration dùng hạn mức thấp nhất. Số file đang có được giữ nguyên.

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
- `/employee/tickets`: cổng Employee gửi/theo dõi yêu cầu hỗ trợ (cũng mở được từ nút **Yêu cầu hỗ trợ** trên Desktop sau khi đăng nhập).
- `/admin/tickets`: Admin lọc, xem chi tiết, trao đổi, thêm ghi chú nội bộ và chuyển trạng thái ticket.

Giao diện nhân viên nằm chung React SPA trong `frontend/admin`; `frontend/employee` chưa có ứng dụng riêng.
`?demo=1` là dữ liệu mô phỏng trong trình duyệt; dùng `?demo=0` để trở lại API thật. Không dùng kết quả demo làm bằng chứng lưu file thật trên server.

## Python Agent — legacy, tùy chọn

Xem [agent/README.md](agent/README.md). Chạy `serve-files` với state và root riêng theo Real/Simulated. Enrollment không tạo tài khoản nhân viên. Không dùng Desktop/Documents thật của Windows làm root.

## Kiểm thử

```powershell
dotnet test backend/ITManagement.slnx
dotnet test backend/ITManagement.slnx --filter "FullyQualifiedName~TicketTests"
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

Smoke 2 trang Admin (Nhật ký thao tác file, Metadata file) dùng fixture API, Edge headless và Vite đang chạy ở 5173:

```powershell
npm --prefix frontend/admin install --no-save --package-lock=false playwright@1.62.1
node frontend/admin/tests/admin-audit-pages.cjs
```

`UI_BASE_URL` mặc định `http://127.0.0.1:5173`; đặt `PLAYWRIGHT_MODULE` nếu module Playwright nằm ngoài `frontend/admin/node_modules`. Lệnh cài chỉ thêm module vào `node_modules`, không đổi `package.json`/lockfile.

Smoke Ticket dùng fixture API (không tạo dữ liệu thật):

```powershell
node frontend/admin/tests/tickets.cjs
```

`tickets-live.cjs` kiểm thử luồng thật Employee → Admin qua HTTP và tạo user/ticket trong database đang cấu hình. Chỉ chạy khi API trỏ database test GUID riêng (không dùng `ITManagement`), sau đó dừng API và drop đúng database test:

```powershell
node frontend/admin/tests/tickets-live.cjs
```

Các file `backend/docker-compose.yml`, Dockerfile và test reverse-proxy cổng 8088 là tài liệu/công cụ cũ; không cần chạy trong cấu hình Windows này. Chạy test giao diện thật bằng các smoke script chỉ với database demo/test, vì chúng tạo dữ liệu.

## Tài liệu và luồng cũ

- [Lịch sử nghiệm thu và demo Agent](legacy/README.md): giữ để tra cứu, không phải hướng dẫn demo hiện tại.
- [Python Agent](agent/README.md): phần mở rộng cũ, không cần chạy cho mô phỏng web.
- Storage cũ theo quota byte vẫn được giữ cùng dữ liệu/API. Luồng mới dùng profile theo người–máy và quota số file; chưa hợp nhất hay xóa storage cũ.
- Hạng mục chuyển file Agent cũ đã khép lại theo quyết định bỏ qua ngày 26/11/2026.
