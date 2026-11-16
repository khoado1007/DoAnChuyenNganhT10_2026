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

Mở http://localhost:5173. URL gốc tự chuyển đến `/employee`; Vite chuyển `/api` sang API cổng 5038.
- `/employee`: cổng nhân viên và đăng nhập nhân viên nếu chưa có phiên.
- `/admin`: nhập trực tiếp URL để mở cổng Admin, nền navy và thẻ đăng nhập trắng. Không có liên kết sang Admin ở cổng nhân viên/trang chọn máy. Chỉ tài khoản Admin đăng nhập tại cổng này.
- `/login`: đường dẫn đăng nhập tương thích; tài khoản sẽ được đưa đến khu vực theo vai trò.
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

## Kiểm tra giao diện khôi phục phiên — 12/11/2026

Cổng nhân viên có nút **Chọn máy làm việc**. Sau khi đăng nhập máy, F5 khôi phục phiên trong cùng tab khi API xác nhận quyền hợp lệ. Lỗi kết nối có nút Thử lại; hết hạn/thu hồi quyền yêu cầu đăng nhập lại. Nội dung chưa bấm Lưu không được giữ qua F5.

Chạy Vite ở cổng 5192 trong terminal riêng, rồi chạy browser smoke (API giả lập, không ghi database):

```powershell
npm --prefix frontend/admin run dev -- --port 5192 --host 127.0.0.1
# Terminal khác; cần Playwright và Microsoft Edge.
# Nếu Playwright không có trong node_modules, đặt PLAYWRIGHT_MODULE tới đường dẫn module đã cài.
node frontend/admin/tests/session/machine-refresh.cjs
```

Có thể đặt `ITAM_UI_URL` khi dùng cổng khác, `ITAM_SCREENSHOT_DIR` khi cần ảnh mobile. Test này không thay thế nghiệm thu với API và Python Agent thật.

## Nghiệm thu luồng file thật — 15/11/2026

Đã kiểm tra bằng Edge + API HTTP + LocalDB + Python `serve_files` thật (mode `simulated`), trên database và thư mục test riêng: **8/8 ca đạt** gồm portal/chọn máy/đăng nhập, tạo/đọc/sửa/lưu file, F5 giữ phiên, quota đồng thời, chặn nhân viên khác, logout, thu hồi quyền và đối chiếu nội dung file trên ổ đĩa.

Agent kiểm thử đã dừng; kết quả này không có nghĩa Agent đã được đăng ký cho tài sản hiện có của bạn. Để sử dụng với dữ liệu của bạn, tạo/chọn tài sản, cấp enrollment token đúng mode và chạy `enroll`, sau đó `serve-files` theo [hướng dẫn Agent](agent/README.md). Không đưa token vào command line. Real inventory, khởi động lại Agent và các thao tác file còn lại chưa nằm trong đợt nghiệm thu này.

Đợt mở rộng cùng ngày: **9/9 ca đạt** cho đổi tên, sao chép, di chuyển, xóa, khôi phục, nhập CSV, restart tiến trình Python dùng lại state/root, replay request đã xác nhận và đối chiếu file trên đĩa. Restart thực hiện khi thao tác đã hoàn tất; chưa nghiệm thu crash giữa mutation, restart API hoặc nhập Office/PDF binary. Không cần sửa mã ứng dụng trong đợt này.

## Đối soát profile bị gián đoạn — 16/11/2026

Trong **Admin → Quota profile**, cột **Giữ chỗ** và **Trạng thái** cho biết thao tác chưa được xác nhận. Chọn **Đối soát → Yêu cầu đối soát** để Agent kiểm tra metadata của vùng lưu trữ hiện có. Khi thành công, số file/quota được cập nhật và reservation của thao tác cũ được giải phóng. **Nhật ký đối soát** hiển thị 20 sự kiện mới nhất với người yêu cầu và thời gian.

- Agent không xóa file, không đọc nội dung tài liệu để gửi cho Admin và không thực hiện lại lệnh bị gián đoạn.
- Nếu Agent offline, storage thiếu, không đọc được thư mục hoặc trả metadata không hợp lệ, quota giữ chỗ vẫn được giữ. Giữ đúng state/root của Agent gốc; không xóa state hoặc tăng quota để coi như đã đối soát.
- Chờ ít nhất 15 giây trước khi yêu cầu lại nếu một attempt còn đang chờ. Kết quả cũ không được ghi đè attempt mới.
- Sau đối soát, nhân viên kiểm tra danh sách file rồi thực hiện thao tác mới nếu cần. Request cũ trả `OPERATION_RECONCILED`, không được tự replay thành công.
- Cần chạy API và Agent cùng phiên bản hỗ trợ đối soát. Không cần migration mới: chỉ lưu metadata điều khiển vào trường hiện có, không lưu body tài liệu.

### Bộ kiểm thử tái lập

```powershell
.\.test-venv\Scripts\python.exe agent\profile_e2e.py --suite all
```

Chuẩn bị Playwright/Edge, `sqlcmd`, LocalDB và dependency theo [README bộ kiểm thử live](frontend/admin/tests/live/README.md). Runner tạo DB GUID riêng, build API riêng, tự chạy/dừng API/Vite/Agent/Edge và lưu summary/log/ảnh trong `artifacts/profile-e2e/`. Không dùng database nghiệp vụ. Các kết quả cũ phía trên là mốc lịch sử; trạng thái kiểm thử mới nhất nằm trong [file tiến độ](TIEN_DO_DO_AN.md).

### Kịch bản demo ngắn

1. Admin: tạo nhân viên, tài sản; cấp quyền truy cập máy và enrollment token `simulated`.
2. Enroll Agent vào state riêng rồi chạy `serve-files` với root ứng dụng riêng theo hướng dẫn Agent.
3. Nhân viên: `/employee` → **Chọn máy làm việc** → Domain ID `COMPANY\<username>` → tạo TXT, lưu, F5, kiểm tra nội dung.
4. Đổi tên/sao chép/di chuyển/xóa/khôi phục; kiểm tra số file và quota; đăng xuất.
5. Minh họa sự cố/đối soát bằng suite `recovery` trên DB thử. Không cố tình gây sự cố với tài liệu đang sử dụng.

Admin chỉ quản lý metadata/quota, không có màn hình đọc nội dung file cá nhân. Đây là mô phỏng domain trong ứng dụng, không phải đăng nhập Windows/Active Directory thật.
