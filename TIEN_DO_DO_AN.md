# Tiến độ và bàn giao đồ án IT Management

**Cập nhật gần nhất:** 12/11/2026 — giờ Việt Nam.
**Workspace đúng:** `D:\DoAnChuyenNganhT10_2026` (đường dẫn cũ `D:\DoAnChuyenNganh` không tồn tại).
**Điểm tiếp tục:** Đã áp dụng nhận diện xanh-trắng, logo STU và font Be Vietnam Pro; URL gốc mặc định vào `/employee`, còn Admin truy cập riêng qua `/admin`. Việc tiếp theo là nghiệm thu API/Agent thật và tiếp tục các chức năng còn thiếu; demo không thay cho nghiệm thu vận hành thật.

## 1. Cách dùng file này

Theo yêu cầu của người dùng, đây là file bàn giao khi phiên làm việc gần hết giới hạn, không phải báo cáo tự động hằng ngày.

- Đọc file này và kiểm tra `git status --short` trước khi tiếp tục ở phiên mới.
- Cập nhật sau mỗi phần việc lớn và trước khi dừng do giới hạn nếu còn khả năng ghi file. Không chờ đến giới hạn cuối cùng vì phiên có thể bị ngắt đột ngột.
- Ghi rõ đã xong, đang dở, file đã sửa, kiểm thử, lỗi còn lại và thao tác tiếp theo. Không biến dự kiến thành việc đã hoàn thành.
- Khi làm nhiều lần trong ngày, cập nhật trạng thái hiện tại và bổ sung nhật ký; giữ lịch sử có ý nghĩa.
- Không chạy lại toàn bộ công việc đã hoàn thành nếu không có thay đổi hoặc bằng chứng cần kiểm tra lại.
- Không có lịch tự động hay automation được tạo cho yêu cầu này.

## 2. Các quyết định đã chốt với người dùng

- Dùng SQL Server Express **LocalDB**: `(localdb)\MSSQLLocalDB`, Windows Authentication. Người dùng đã kết nối SSMS thành công.
- Không dùng Docker. Các file Docker cũ được giữ và đánh dấu legacy.
- Người dùng tự quản lý cài đặt/dịch vụ SQL Server; không tự cài lại, sửa cấu hình hệ thống, xóa instance hoặc dữ liệu.
- Giữ database nghiệp vụ `ITManagement`; chỉ tạo database riêng cho kiểm thử.
- Hỏi trước những lựa chọn chưa rõ, không đoán ý người dùng. Không hỏi lại quyết định đã chốt.
- F5 ở Desktop phải khôi phục phiên của đúng máy nếu backend xác nhận phiên/quyền còn hợp lệ (chốt 12/11/2026).
- Frontend hiện là một React SPA tại `frontend/admin`; folder `frontend/employee` trống. Chưa có quyết định tách thành hai ứng dụng.
- Nhận diện giao diện: nền trắng `#FFFFFF`, bảng 8 sắc xanh dương do người dùng cung cấp, sử dụng logo/icon có sẵn trong `img/`; chi tiết ở mục 6.1. Không tự đổi tên hệ thống hay suy đoán thông tin trường/tác giả.
- Phạm vi chính: ITAM, mô phỏng đăng nhập máy, profile theo người dùng–thiết bị, file local qua Agent và quota số file. Ticket là phần bổ sung ưu tiên sau.

## 3. Đã hoàn thiện trong phiên 11/11/2026

### Rà soát hiện trạng

Đã đọc cấu trúc, README, các module chính và bộ test. Ghi nhận:
- Có UI quản trị tài khoản, tài sản, Agent, quota và dashboard.
- Có cổng `/employee` hiển thị hồ sơ và tài sản được cấp.
- Có `/machines` cho chọn máy, đăng nhập, Desktop/Documents và thao tác file.
- Có nghiệp vụ phân quyền, cấp phát, access grant, phiên máy, profile và quota trong backend.
- Những phần có mã nguồn không mặc nhiên được xem là đã nghiệm thu toàn bộ bằng trình duyệt và Agent thật.

### Chuyển backend sang SQL Server LocalDB

Đã thực hiện:
- Thay truy vấn PostgreSQL `FOR UPDATE` bằng khóa SQL Server `UPDLOCK, ROWLOCK`, giữ thứ tự khóa hiện có.
- Chuyển xử lý lỗi unique key sang mã SQL Server 2601/2627.
- Chuyển cấp số ticket sang `NEXT VALUE FOR`; lấy kết quả trước khi gọi `Single` để không tạo subquery không hợp lệ.
- Gỡ dependency Npgsql khỏi project Infrastructure; giữ migration PostgreSQL cũ ngoài phần biên dịch.
- Khôi phục snapshot SQL Server từ model của migration `20261110140837_InitialSqlServer`; không sửa migration đã có.
- Chuyển integration test sang database SQL Server có tên GUID riêng; migrate và seed riêng.
- Bổ sung test snapshot khớp model và migrate lại không mất dữ liệu đã seed.
- Cập nhật README, hướng dẫn Agent, tạo `RUNNING.md` cho cách chạy Windows trực tiếp.
- Đánh dấu Compose/Dockerfile cũ là legacy, không xóa chúng.
- Có một lượt review độc lập chỉ đọc: không phát hiện vấn đề mới cần sửa.

Các file chính:
- `backend/src/ITManagement.Infrastructure/Services/`: AgentService, AssetService, AssignmentService, DeviceSessionService, FileService, ProfileOperationService, StorageAdminService, UserService.
- `backend/src/ITManagement.Infrastructure/Persistence/Repositories/TicketRepository.cs`.
- `backend/src/ITManagement.Infrastructure/Persistence/SqlServerMigrations/AppDbContextModelSnapshot.cs` — file mới.
- `backend/src/ITManagement.Infrastructure/ITManagement.Infrastructure.csproj`.
- `backend/tests/ITManagement.IntegrationTests/`: AuthTests, DashboardSqlServerTests, FileUploadTests, FoundationTests, SqlServerMigrationTests.
- `README.md`, `RUNNING.md`, `agent/README.md`, `.gitignore` và chú thích legacy trong các file Docker.

## 4. Bằng chứng kiểm thử

| Kiểm tra | Kết quả gần nhất | Ý nghĩa / giới hạn |
| --- | --- | --- |
| Backend sau chuyển đổi | **82/82 đạt** | Chạy trên SQL Server LocalDB thật, database test riêng |
| Snapshot và kiểm tra nền tảng | **3/3 đạt** | Test snapshot từng thất bại trước khi bổ sung snapshot, sau đó đạt |
| Review độc lập | Không có phát hiện cần sửa | Review mã và kết quả test, không chạy lại database test |
| `git diff --check` | Đạt | Không có lỗi whitespace tại thời điểm kiểm tra |
| Frontend `npm test` ngày 12/11 | **23/23 đạt** | Bao gồm 3 test mới cho mã phiên: đúng máy, dữ liệu lỗi, tách tab/demo |
| Frontend `npm run build` ngày 12/11 | Thành công | Vẫn có cảnh báo bundle lớn |
| Edge smoke ngày 12/11 | Đạt | Portal → máy → Desktop, F5, 503/thử lại, 403, 401, cách ly máy, logout và cleanup khởi tạo lỗi; API giả lập, chưa nghiệm thu Agent thật |
| Python Agent | **33 đạt, 0 lỗi, 1 bỏ qua** | Chạy lại trên Windows sau khi sửa ACL cho state và profile |
| Deployment smoke cũ | 4 lỗi kết nối cổng 8088 | Không có dịch vụ Docker tại cổng này; không thuộc cách chạy hiện tại |

Lệnh backend đã chạy thành công:

```powershell
dotnet test backend/ITManagement.slnx --no-restore --logger "console;verbosity=normal"
```

Log tại `artifacts/sqlserver-suite.log` (file local, bị Git bỏ qua). Lần chạy sử dụng biến `TEST_SQLSERVER` trỏ tới named pipe đang hoạt động của chính LocalDB, với Windows Authentication. Alias `(localdb)\MSSQLLocalDB` không được phân giải đúng từ tiến trình Codex dù SQL Server đang chạy và SSMS kết nối được. Kết nối qua named pipe đã thành công, không sửa cài đặt SQL Server. Không tái sử dụng địa chỉ pipe cũ: nó có thể đổi sau khi instance khởi động lại.

Database test dùng tiền tố `itmanagement_test_` hoặc `itam_dashboard_test_`, có GUID; không migrate hay xóa database `ITManagement`.

## 5. Các phần còn thiếu / chưa xử lý

| Ưu tiên | Phần việc | Bằng chứng hiện tại |
| --- | --- | --- |
| 2 | Hoàn thiện luồng nhân viên | Cổng `/employee` chưa có lối vào màn hình chọn máy; không tự tách ứng dụng chỉ vì folder employee trống |
| 2 | Nghiệm thu khôi phục phiên với API thật | Frontend F5 đã sửa và qua browser smoke với API giả lập; chưa chạy chuỗi API + Agent thật trong phiên 12/11 |
| 2 | Nhật ký tổng hợp và metadata file cho Admin | Có audit trong backend nhưng chưa có màn hình tra cứu đầy đủ |
| 3 | Ticket cơ bản | Có model/repository, UI đang thông báo chưa triển khai; chưa có luồng gửi và xử lý hoàn chỉnh |
| 3 | Thống nhất phần storage cũ và mới | API quota byte/lưu file server còn song song với profile Agent/quota số file; cần trao đổi trước khi loại bỏ hoặc thay đổi |
| 3 | Nghiệm thu end-to-end và tài liệu | Chưa chạy toàn bộ kịch bản trình duyệt + Real/Simulated Agent sau chuyển đổi |

Một số skill tham chiếu các tài liệu chưa tìm thấy (`STORAGE_POLICY.md`, `DEMO_ACCEPTANCE.md`, v.v.). Không giả định nội dung các file này hoặc xem skill là bằng chứng chức năng đã hoàn thành.

## 6. Bước tiếp theo cụ thể

1. Đã đọc trạng thái Git và file này; giữ nguyên thay đổi SQL Server, Agent ACL, portal và F5 hiện có.
2. **Đã cá nhân hóa frontend** theo mục 6.1 bằng cách cập nhật SPA hiện tại, không viết lại ứng dụng.
3. Đã xem logo trong `img/`; giữ tên IT Desk, dùng ảnh STU có sẵn và không tự thêm thông tin tác giả/trường chưa cung cấp.
4. Đã áp dụng theme trắng-xanh, logo trên trang đăng nhập/layout/portal/mô phỏng máy và favicon. Không đổi luồng nghiệp vụ hay phân quyền.
5. Đã chạy frontend tests/build và kiểm tra giao diện bằng trình duyệt demo; chi tiết và giới hạn được ghi tại mục 7.
6. Tiếp tục nghiệm thu Employee → chọn máy → đăng nhập → tạo file → F5 → đăng xuất với API/Agent thật trên database và storage test riêng.
7. Các màn hình nhật ký/metadata Admin, ticket và hợp nhất storage cũ/mới vẫn cần trao đổi phạm vi; không tự loại bỏ API/dữ liệu cũ hoặc cấp quyền đọc file cá nhân cho Admin.

### 6.1. Yêu cầu cá nhân hóa frontend — chốt ngày 12/11/2026

**Trạng thái:** Đã áp dụng trong lượt xử lý giao diện ngày 12/11/2026. Đây là cập nhật trên SPA hiện có, không thay đổi nghiệp vụ.

**Mục tiêu:** Giao diện mang dấu ấn trường/tác giả hơn, chủ đạo xanh dương và nền trắng; nâng cấp trên frontend hiện có thay vì viết lại toàn bộ.

**Bảng màu người dùng cung cấp:**

| Sắc xanh | HEX | RGB | CMYK (theo thông tin người dùng) |
| --- | --- | --- | --- |
| Xanh dương pastel | `#BCD9F2` | 188, 217, 242 | 22, 18, 0, 5 |
| Xanh da trời | `#6CB8F8` | 108, 184, 248 | 55, 23, 0, 6 |
| Xanh nước biển | `#1E7FC8` | 30, 127, 200 | 85, 37, 0, 20 |
| Xanh lam | `#1F6FEB` | 31, 111, 235 | 87, 53, 0, 8 |
| Xanh coban | `#0047AB` | 0, 71, 171 | 100, 58, 0, 33 |
| Xanh jean | `#3F6C9E` | 63, 108, 158 | 60, 32, 0, 38 |
| Xanh xám | `#6B8299` | 107, 130, 153 | 30, 15, 0, 40 |
| Xanh navy | `#1B2A4A` | 27, 42, 74 | 64, 43, 0, 71 |

- Nền chính: trắng `#FFFFFF`.
- Khi triển khai web, dùng mã HEX làm tham chiếu; RGB/CMYK ở bảng là dữ liệu người dùng cung cấp, chưa kiểm chứng chuyển đổi màu in ấn.
- Phương án áp dụng trong lượt này: trắng làm nền chính; navy `#1B2A4A` cho cấu trúc/tiêu đề; cobalt `#0047AB` cho hành động; các xanh `#1F6FEB`, `#1E7FC8`, `#3F6C9E`, `#BCD9F2`, `#6CB8F8` và xanh xám làm điểm nhấn/nền phụ, không dùng ngang nhau.
- Kiểm tra độ tương phản chữ/nút; giữ màu trạng thái lỗi/cảnh báo/thành công rõ nghĩa, không tự đổi mọi trạng thái thành xanh.
- Tiếp tục tuân thủ yêu cầu màu phẳng, không gradient trong README hiện có.

**Tài nguyên đã tìm thấy ở workspace:**

- `img/logoSTU.zip` — gói ZIP gốc, giữ nguyên.
- `img/logoSTU/favicon.ico`.
- `img/logoSTU/favicon-16x16.png`, `img/logoSTU/favicon-32x32.png`.
- `img/logoSTU/apple-touch-icon.png`.
- `img/logoSTU/android-chrome-192x192.png`, `img/logoSTU/android-chrome-512x512.png`.

Đường dẫn tuyệt đối thư mục: `D:\DoAnChuyenNganhT10_2026\img`. Đã xem hình ảnh và dùng nguyên bộ icon có sẵn; không sửa hoặc giải nén đè lên nguồn. Đã sao chép các bản cần cho ứng dụng vào `frontend/admin/public/brand/` để Vite phục vụ khi chạy và đóng gói.

**Phạm vi nhận diện áp dụng trong lượt này:**

- Giữ tên IT Desk; dùng ảnh STU trên trang đăng nhập, sidebar, portal, mô phỏng máy và bộ favicon.
- Không thêm tên trường/khoa, tác giả, MSSV, lớp/giảng viên, trang giới thiệu hoặc footer cá nhân khi chưa có nội dung được cung cấp.

**Các file cập nhật:** `frontend/admin/src/main.tsx` (Ant Design theme), `src/styles.css`, `src/pages/LoginPage.tsx`, `src/layouts/AdminLayout.tsx`, `src/pages/PortalPage.tsx`, `frontend/admin/index.html` và tài nguyên trong `frontend/admin/public/brand/`.

## 7. Trạng thái bàn giao hiện tại

- Phần SQL Server và Agent đã nằm trong commit `de4f33a` khi bắt đầu phiên 12/11. Thông tin “chưa commit” của ngày trước không còn phản ánh HEAD hiện tại.
- Thay đổi frontend ngày 12/11 và file tiến độ đang ở workspace, chưa commit/push trong phiên này.
- Không thay đổi database/backend/Agent trong phiên 12/11. Không chạy lại bộ backend 82 test hoặc Agent 33 test vì không thay mã các phần đó.
- Đã cập nhật PortalPage, MachinesPage, auth.ts; thêm device-session.ts, device-session.test.mjs và tests/session/machine-refresh.cjs.
- Đã chạy `npm --prefix frontend/admin test`: 23/23 đạt; `npm --prefix frontend/admin run build`: thành công.
- Đã cá nhân hóa giao diện: nền trắng, navy/cobalt theo bảng màu, Be Vietnam Pro trên toàn bộ giao diện; STU logo trên login, sidebar, portal và workstation; favicon dùng tài nguyên có sẵn. Tên IT Desk được giữ, không tự thêm thông tin tác giả/trường.
- Tài nguyên frontend được sao chép từ `img/logoSTU/` sang `frontend/admin/public/brand/`; nguồn gốc trong `img/` không bị sửa.
- Kiểm tra mới: `npm.cmd --prefix frontend/admin test` **23/23 PASS**; `npm.cmd --prefix frontend/admin run build` **PASS** (còn cảnh báo chunk JavaScript lớn hơn 500 kB); `git diff --check` **PASS**.
- Kiểm tra trình duyệt với dữ liệu DEMO: login/Admin ở desktop và viewport hẹp, chọn máy, đăng nhập `COMPANY\\khoa`, mở File Manager trên PC-001; logo tải được, quota demo hiển thị `0/10`. Ở viewport hẹp 375px, document không tràn ngang.
- Font Be Vietnam Pro đã được đóng gói nội bộ với các weight 400/500/600/700, dùng đồng nhất toàn bộ shell; Noto Serif không còn là dependency.
- URL gốc `/` mặc định chuyển tới `/employee`; `/admin` giữ cổng Admin riêng, link “Quản trị” từ workstation trỏ `/admin`; logout/phiên hết hạn giữ đúng khu vực tương ứng.
- Browser smoke mới xác nhận `/` chuyển tới `/employee`, `/admin` hiển thị đúng khu vực quản trị và `document.fonts.check('16px "Be Vietnam Pro"')` trả về true.
- Giới hạn kiểm tra giao diện: đây là preview dùng dữ liệu demo trong bộ nhớ; không nghiệm thu API thật/Agent thật. Ảnh browser được xem trực tiếp nhưng không lưu thành file trong repo.
- Browser smoke dùng Edge headless và API giả lập; 401/403 không hiển thị profile, 503 giữ mã phiên để thử lại, F5 không tạo phiên máy mới, đổi máy không tái sử dụng profile sai.
- Test khởi tạo profile thất bại xác minh yêu cầu logout còn token và chỉ xóa token sau khi đóng phiên thành công.
- Mã tham chiếu chỉ lưu `{deviceId, sessionId}` trong sessionStorage của tab, tách live/demo; không lưu nội dung file hoặc profile vào cache.
- Review độc lập: không phát hiện vấn đề mới cần sửa.
- Ảnh kiểm tra mobile: `docs/session-check/employee-mobile.png` (artifact local, không được Git theo dõi).
- Giới hạn: demo backend nằm trong bộ nhớ trình duyệt nên F5 không bảo toàn dữ liệu demo như server thật; cơ chế này không lưu bản thảo TXT/CSV chưa bấm Lưu. Nghiệm thu toàn luồng với Agent thật vẫn chưa thực hiện.

## 8. Nhật ký

### 11/11/2026

- Rà soát tổng thể và xác nhận folder employee trống nhưng UI nhân viên nằm trong SPA hiện có.
- Chốt LocalDB và bỏ Docker khỏi quy trình chạy.
- Hoàn thiện chuyển đổi backend/database, kiểm tra 82/82 test đạt.
- Tái hiện lỗi lưu Agent trên Windows: `getpass.getuser()` chỉ trả username ngắn nên `icacls` tạo ACL sai; thay bằng account đầy đủ từ `whoami` cho state và thư mục nội bộ profile. Giữ ACL chỉ cấp cho đúng tài khoản, không nới quyền.
- Kiểm tra lại Agent: **33 PASS, 1 SKIP**; `git diff --check -- agent/itam_agent.py agent/local_profiles.py` PASS.
- Bước tiếp theo: hoàn thiện lối vào màn hình chọn máy từ `/employee`.
- Tạo file tiến độ này để phiên sau tiếp tục từ phần Python Agent.

### 12/11/2026 — hoàn thành phần portal và F5

- Đã đọc lại file, xác nhận Agent đã được sửa trong commit hiện tại `de4f33a`; workspace sạch lúc bắt đầu. Không làm lại phần ACL.
- Người dùng chốt: F5 phải giữ phiên và mở lại Desktop đúng máy nếu quyền vẫn hợp lệ.
- Đã thêm nút “Chọn máy làm việc” cho Employee trong PortalPage, giữ một SPA.
- Đang tạo test trình duyệt `frontend/admin/tests/session/machine-refresh.cjs` cho F5, mất mạng, thu hồi quyền và logout; chưa kết luận tính năng hoàn thành.
- Vite kiểm thử đang chạy cổng 5192, chỉ mock API, không đụng database nghiệp vụ.

- Mốc giữa phiên: test trình duyệt đã tái hiện mất Desktop sau F5 trước sửa, sau sửa đạt các ca khôi phục, 503/thử lại, cách ly máy, 403 và logout. Unit frontend 23/23 đạt. Đang mở rộng kiểm tra 401, lỗi khởi tạo profile và kiểm tra build/review cuối.

- Kết thúc mốc: hoàn thiện lối vào `/machines`, khôi phục mã phiên qua GET profile, kiểm tra quyền trước hiển thị và xử lý lỗi/cleanup.
- Kết quả cuối: 23 unit tests PASS, build PASS, browser smoke PASS trên Edge gồm 401 và cleanup khi ensure lỗi. Kiểm tra ảnh mobile không tràn ngang. Review không có phát hiện cần sửa.
- Các ghi chú “đang thực hiện” bên trên là lịch sử giữa phiên, không phải việc còn dở.
- Server Vite kiểm thử cổng 5192 đã được dừng sau khi hoàn tất; không để tiến trình kiểm thử chạy nền.

### 12/11/2026 — bổ sung bước cá nhân hóa frontend

- Người dùng cung cấp 8 mã xanh dương, yêu cầu nền trắng và dùng logo/icon trong `img/`.
- Đã tìm thấy `img/logoSTU.zip` cùng bộ PNG/ICO trong `img/logoSTU/`; chưa mở/xử lý ảnh hoặc giải nén ZIP.
- Đã nâng cá nhân hóa frontend thành bước tiếp theo, giữ các việc còn thiếu trước đó trong kế hoạch.
- Lượt này chỉ cập nhật `TIEN_DO_DO_AN.md`; chưa thay đổi giao diện hoặc kết quả kiểm thử trước đó.

### 12/11/2026 — áp dụng nhận diện giao diện

- Đọc lại tiến độ, đối chiếu đặc tả frontend và kiểm tra các thay đổi chưa commit; giữ nguyên các phần API/Agent và luồng F5.
- Dùng logo STU có sẵn, giữ tên IT Desk, nền trắng và phối navy/cobalt; không thêm thông tin tác giả/trường chưa được cung cấp.
- Cập nhật Ant Design theme/CSS, trang đăng nhập, sidebar Admin, portal nhân viên/kỹ thuật viên, header mô phỏng máy, favicon và metadata tiêu đề/màu trình duyệt.
- Kiểm thử frontend: **23/23 PASS**; build/type-check: **PASS** với cảnh báo bundle lớn; `git diff --check`: **PASS**.
- Kiểm tra browser DEMO ở Admin, màn hình chọn máy và Desktop/File Manager; logo hiển thị, responsive hẹp 375px không tràn ngang. Không lưu ảnh screenshot vào repo.
- Chưa xác minh bằng API/Agent thật; đây vẫn là nghiệm thu giao diện trên demo.
- Theo yêu cầu tiếp theo, đã thay Georgia/Segoe UI UI text bằng Be Vietnam Pro cho toàn bộ màn hình, đóng gói font nội bộ; URL gốc vào cổng nhân viên và `/admin` vào cổng quản trị riêng.
