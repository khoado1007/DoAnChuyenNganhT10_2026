# Tiến độ và bàn giao đồ án IT Management

**Cập nhật gần nhất:** 17/11/2026 — giờ Việt Nam.
**Workspace đúng:** `D:\DoAnChuyenNganhT10_2026` (đường dẫn cũ `D:\DoAnChuyenNganh` không tồn tại).
**Điểm tiếp tục:** ĐÃ hoàn tất luồng mô phỏng web không Agent: Employee active dùng mọi máy active, chọn máy trước login, thông tin/phần mềm chỉ đọc và file riêng người–máy trên server. Backend96/96, frontend32/32, E2E SQL/API/Edge không Agent9/9, buildPASS. Đã backup/verify và áp dụng ServerSimulation + SeparateServerAndAgentProfiles; API5038/UI5173 đang chạy. 5 tài sản,4profile Agent,1file cũ giữ nguyên; server tạo profile riêng nên không chặn login. User chọn chuyển file cũ SAU. Đã dọn hướng dẫn legacy và chốt kế hoạch mục5–6. Bước tiếp: Nhật ký Admin chỉ đọc trước, metadata sau; không tự chuyển/xóa dữ liệu Agent cũ.

## 1. Cách dùng file này

**Bổ sung yêu cầu 18/11:** Employee chưa có trang **Thông tin tài khoản** để tự đổi mật khẩu. Đã ghi vào bước vòng đời mật khẩu trong `docs/PLAN_PASSWORD_SOFTWARE.md`: thêm điểm truy cập từ khu vực Employee, xem thông tin tài khoản và chủ động đổi mật khẩu ngoài popup nhắc. Hiện chỉ ghi kế hoạch, chưa triển khai; chưa mở rộng sang sửa hồ sơ cá nhân.

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

**Mốc mới nhất — 16/11/2026:** backend **84/84 PASS**, Agent **46 PASS / 1 SKIP**, frontend **29/29 PASS**, live HTTP/SQL/Python/Edge **26/26 kiểm tra PASS** (core8, files12, recovery6). Frontend build và API build PASS. Review độc lập không còn finding cần sửa. Cảnh báo chunk >500kB và cảnh báo restore AutoMapper vẫn được ghi nhận; chưa xử lý dependency trong phiên này.

Log cuối: `docs/profile-live/backend-final.log`, `agent-final.log`, `frontend-tests.log`, `frontend-build.log`, `live-final.log`; các file docs/log là local bị bỏ qua Git. Runner và test source đã chuyển vào repo, xem RUNNING.md.

Các dòng phía dưới là bằng chứng mốc lịch sử trước đợt mở rộng:

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

| Ưu tiên | Phần việc | Trạng thái |
| --- | --- | --- |
| Tiếp theo | Nhật ký tổng hợp và metadata file cho Admin | User đã đồng ý phạm vi chỉ đọc: lọc theo thời gian/người/thao tác, xem tên/dung lượng/chủ sở hữu/máy; không đọc nội dung file |
| Sau nhật ký | Ticket cơ bản | Chưa có luồng hoàn chỉnh; cần chốt trạng thái và cách phân công trước khi triển khai |
| Chờ user | Chuyển file Agent cũ | User chọn giữ nguyên, chuyển sau; cần đúng thư mục nguồn, không tự dò/copy/xóa dữ liệu |
| Đã dọn tài liệu | Storage/Agent legacy | Hướng dẫn cũ ở legacy/README.md; API và dữ liệu giữ nguyên, chưa gộp hoặc loại bỏ |
| Ngoài phạm vi hiện tại | Inventory thật / nhiều máy vật lý | Mô phỏng web không cần phần này; chỉ mở lại khi user yêu cầu |

Luồng mô phỏng server đã đạt: backend96/96, frontend32/32, E2E không Agent9/9 tại mốc17/11. Các kết quả Agent cũ chỉ là lịch sử.

## 6. Kế hoạch tiếp theo — làm từng mốc nhỏ

1. **Nhật ký Admin trước:** API chỉ đọc có phân trang và lọc ngày/người/thao tác; lấy sự kiện đã có, thêm menu Nhật ký. Test401/403, bộ lọc, thứ tự và phân trang. Không trả token/nội dung file.
2. **Metadata file sau:** bảng tên file, thư mục, dung lượng, tài khoản, máy và loại kho Server/Agent. Tách rõ dữ liệu cũ/mới, không có nút mở/tải file cá nhân cho Admin. Test ranh giới quyền và dữ liệu trống.
3. **Nghiệm thu mỗi mốc:** test phần liên quan + build + kiểm tra browser, cập nhật file tiến độ rồi mới mở bước kế tiếp. Không làm cả Ticket khi gần hết giới hạn.
4. **Ticket:** sau hai mốc trên, trao đổi nghiệp vụ gửi/nhận/phân công/đóng yêu cầu rồi triển khai.
5. **File cũ:** chỉ chuyển khi người dùng cung cấp nguồn và yêu cầu; giữ nguyên bản gốc và backup.

Dọn cache17/11 chưa thực hiện: lệnh xóa17 file cache (~179KiB) bị bộ kiểm soát tự động chặn theo chính sách. Không thử cách khác để vượt chặn. Không ảnh hưởng ứng dụng.

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

**Hiện tại 16/11:** thay đổi trong workspace chưa commit/push; gồm Agent/API/UI đối soát, regression tests, bộ chạy live và tài liệu. Main API đã restart bằng binary mới, xác nhận Swagger có endpoint đối soát/nhật ký và `/api/machines`/UI trả200. Không còn test server5189/5190; không có Agent gắn vào dữ liệu nghiệp vụ. Các ghi chú12/11 phía dưới là lịch sử, trạng thái kết thúc mới nhất nằm ở nhật ký16/11.

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

### 15/11/2026 — tiếp tục chẩn đoán đăng nhập chậm

- Bắt đầu từ HEAD `636fca6`, workspace sạch. Các ghi chú “chưa commit” ngày 12/11 ở trên là lịch sử, không phải trạng thái hiện tại.
- Người dùng xác nhận đã mở SSMS/kết nối LocalDB. Không cài/sửa dịch vụ SQL, không dùng Docker, không tác động dữ liệu `ITManagement`.
- Đọc lại log `artifacts/login-diagnostic-2026-11-12.log`: 2/2 test đạt nhưng các request login thành công mất 11,46–20,25 giây. Đây là thời gian integration run cũ, không phải đo click trên máy người dùng.
- Tạo công cụ đo local tại `docs/login-diagnostics/` (bị Git bỏ qua): console gọi AuthService/Identity thật, tạo database GUID riêng, đảo thứ tự pooling qua 9 vòng và xóa database riêng thành công khi kết thúc. Không in mật khẩu/token.
- Kết quả trung vị 8 vòng sau vòng đầu: mở SQL **6,86 ms** khi pooling tắt, **0,12 ms** khi bật; AuthService admin **179,73 / 134,79 ms**; COMPANY alias **151,54 / 144,52 ms**. Hash password khoảng 94–245 ms trong mẫu đo, không giảm độ mạnh hash.
- Pooling giúp giảm chi phí kết nối nhưng dữ liệu hiện tại chưa chứng minh nó gây ra độ trễ 11–20 giây. Không thay appsettings theo suy đoán.
- Chạy lại hai integration test `Domain_alias_uses_existing_identity_and_rejects_other_domains` và `Profile_snapshot_and_file_operation_flow_with_agent_handshake`: **2/2 PASS**, tổng 21,37 giây gồm setup. API login thành công **847/847/443/450 ms**, tạo session **166 ms**, file operation qua handshake giả lập **988 ms**. TEST_SQLSERVER vẫn Pooling=False.
- SQL thật trong phép đo đi qua named pipe của instance đang chạy, Encrypt=False; chưa bao phủ alias `(localdb)` + Encrypt=True của cấu hình ứng dụng, mạng HTTP thật hoặc tốc độ render trình duyệt. COMPANY alias trong console dùng cùng admin để cô lập username; integration test có employee nhưng chưa có Python Agent thật.
- Luồng máy có login → tạo session → ensure Desktop; Agent nghỉ 1 giây sau mỗi vòng poll thành công, backend timeout chờ kết quả 10 giây. Không có nghĩa mọi login đều chờ 10 giây.
- Log/report/source local: `docs/login-diagnostics/results-2026-11-15.log`, `http-tests-2026-11-15.log`, `README.md`, `Program.cs`, `LoginDiagnostics.csproj`. Không tái dùng named pipe cũ sau khi LocalDB khởi động lại.
- Restore công cụ xuất cảnh báo NU1903 cho AutoMapper 16.2.0; ghi nhận để kiểm tra dependency riêng, chưa xác minh ảnh hưởng và không coi đây là nguyên nhân chậm đăng nhập.
- Thay đổi được Git theo dõi trong lượt này: chỉ file tiến độ. Chưa commit/push, chưa sửa mã frontend/backend/Agent hoặc cấu hình runtime.
- Bước kế tiếp: đo Network/Performance trình duyệt trong lúc tái hiện lag, tách POST auth/login, tải JS và request trang đích; đối chiếu log server cùng thời điểm. Sau đó mới chọn thay đổi tối ưu dựa trên điểm nghẽn thực tế. Tiếp tục nghiệm thu API + Python Agent thật trên dữ liệu test riêng.

### 15/11/2026 — chạy ứng dụng cho người dùng

- Đã chạy API cổng 5038 và Vite cổng 5173 bằng tiến trình nền ẩn, giữ chạy để người dùng thử. Không đổi appsettings, migrate/seed hoặc đặt lại tài khoản.
- Kiểm tra HTTP 200: trang Admin, Swagger, API machines qua Vite proxy. API machines đọc SQL bằng cấu hình thực tế, lần đầu khoảng 3,95 giây gồm khởi tạo EF.
- URL: http://127.0.0.1:5173/admin?demo=0 và http://127.0.0.1:5173/employee?demo=0. Dùng tài khoản hiện có. Chưa chạy Python Agent trong lượt này.
- Log tại artifacts/api-local.out.log, api-local.err.log, ui-local.out.log, ui-local.err.log.
- Quy ước mới: trả lời ngắn, chỉ kết quả/thống kê chính và URL/hướng dẫn chạy; không kể quá trình. Đã lưu vào AGENTS.md local và tiến độ.

### 15/11/2026 — nghiệm thu luồng chính với Python Agent thật

- Người dùng xác nhận bước đăng nhập đã ổn và yêu cầu tiếp tục. Không còn coi chậm đăng nhập là lỗi đang chặn; chỉ đo lại nếu tái xuất hiện.
- Bộ kiểm tra local: `docs/profile-live/run.py` và `browser.cjs` (artifact Git bỏ qua). API Kestrel cổng 5189, Vite 5190, Edge headless, LocalDB GUID riêng. Không mock request trình duyệt hoặc handshake Agent.
- Python chạy Agent.serve_files thật, enrollment thật, mode simulated; không thu thập inventory máy người dùng, không đụng Desktop/Documents Windows. File được ghi trong root test riêng.
- **8/8 kiểm tra đạt**: (1) portal → chọn máy → đăng nhập COMPANY → ensure Desktop; (2) tạo/đọc/sửa/lưu file từ UI; (3) F5 giữ đúng session và nội dung; (4) hai yêu cầu tạo đồng thời với quota 2 chỉ một yêu cầu thành công, một 409, usedFiles=2/reservedFiles=0; (5) nhân viên khác bị 403 khi đọc profile/đăng nhập máy chưa cấp quyền; (6) logout xóa token và phiên cũ bị 403; (7) đăng nhập lại, thu hồi quyền rồi F5 không hiển thị file, xóa tham chiếu phiên; (8) kiểm tra file vật lý có đúng nội dung UTF-8 đã lưu.
- Không phát sinh JS pageerror. Đã xem ảnh `docs/profile-live/run-d65ff401a1284cfb83d3075884a99110/desktop-live.png`; log tổng `docs/profile-live/latest.log`, log HTTP trong thư mục run đó.
- Hai lần chuẩn bị đầu lỗi ở harness đọc encoding log SQL/cấu hình JWT, một lần selector nút login không tính icon. Đã sửa harness và chạy lại đầy đủ đạt; không sửa ứng dụng để vượt test.
- Đã xóa đúng database GUID do harness tạo, dừng API/Vite/Agent thử. Giữ file/screenshot/log local để đối chiếu. App người dùng tại 5173 và API 5038 vẫn chạy; không có Agent được gắn vào dữ liệu nghiệp vụ trong lượt này.
- Không sửa mã production hoặc cấu hình hệ thống; chỉ cập nhật tiến độ và hướng dẫn chạy. Không cần chạy lại toàn bộ unit suite do không đổi mã ứng dụng.
- Giới hạn: một host, một Agent mode simulated, chưa test Real inventory/hai máy vật lý, chưa restart Agent hoặc kiểm tra hết rename/move/copy/delete/restore/import. Đây là các ca tiếp theo, không cần làm lại luồng chính đã đạt.

### 15/11/2026 — mở rộng thao tác file và restart Agent

- **9/9 kiểm tra đạt** trên Edge, Kestrel HTTP, LocalDB GUID riêng và Python Agent CLI chạy thành tiến trình thật (mode simulated).
- Kiểm tra qua UI: đổi tên; sao chép Desktop → Documents; di chuyển không tăng quota; xóa vào Recycle giảm quota; khôi phục trả nội dung/quota; nhập CSV giữ nguyên byte.
- Sau khi thao tác đã hoàn tất, dừng tiến trình Python và chạy tiến trình mới bằng cùng state/root. F5 giữ sessionId, list/read qua Agent mới xác nhận nội dung còn nguyên và quota usedFiles=4/reservedFiles=0.
- Gửi lại requestId của mutation create đã được xác nhận trước restart: cùng fileId, không tạo trùng hoặc tăng quota. Đây là kiểm tra replay qua API, không chứng minh journal phục hồi khi crash giữa thao tác.
- Đối chiếu ổ đĩa: Documents/renamed.txt và copy.txt có đúng UTF-8; Desktop/import.csv khớp byte; idempotent.txt chỉ có một bản. Không có JS pageerror. Đã xem ảnh kết quả cuối.
- Harness local: `docs/profile-live/run-extended.py`, `extended.cjs`; chạy bằng `.test-venv/Scripts/python.exe docs/profile-live/run-extended.py` khi LocalDB chạy, cổng 5189/5190 trống, API đã build và Edge/Playwright có sẵn. Harness đọc named pipe từ log LocalDB; không lưu pipe cứng.
- Log: `docs/profile-live/extended-latest.log`; artifact của lượt đạt: `docs/profile-live/run-fb5fbf44238e4b2d9a188c9cf6f83100/`, gồm browser.log, agent-before.log, agent-after.log, extended-live.png. Toàn bộ docs là local bị Git bỏ qua, cần chuẩn hóa nếu muốn chia sẻ bộ chạy.
- Lần đầu bộ đo dừng ở selector Documents do icon ảnh hưởng accessible name; sửa selector trong test, chạy lại toàn bộ 9 ca đạt. Không phát hiện lỗi ứng dụng cần sửa trong đợt này.
- Đã dừng tiến trình thử và xóa đúng database GUID tạo cho lượt chạy. Không chạm dữ liệu ITManagement; app chính vẫn giữ riêng ở 5173/5038.
- Chưa kiểm tra Office/PDF binary import, mất mạng giữa mutation, crash khi đang ghi, restart API, Real-mode inventory hoặc nhiều thiết bị vật lý. Không gộp các mục đó vào kết luận đạt.

### 15/11/2026 — mốc giữa phiên: đóng gói kiểm thử, mở rộng và sửa lỗi

- Đang đóng gói harness vào `agent/profile_e2e.py`, `frontend/admin/tests/live/` thay vì chỉ giữ trong docs; tự tạo/xóa DB GUID, build API ở artifacts riêng, dùng process thật, tạo summary.json và log không chứa credential. Artifact/state được gitignore.
- Hai suite core/files bản đóng gói ban đầu đạt 17 kiểm tra. Bản mở rộng files đạt 12 kiểm tra gồm PDF/DOCX byte fidelity, mở iframe PDF/tải DOCX, API restart và khôi phục sang thư mục khác.
- Phát hiện lỗi restore qua đường dẫn khác làm đổi fileId: 3 regression test fail trước sửa; sửa một dòng index theo destination trong `agent/local_profiles.py`. Agent suite sau sửa **37 PASS, 1 SKIP**.
- Fault injection dùng helper riêng dưới agent/tests (không có hook production): kill Agent ngay sau intent journal trước khi ghi file. Phát hiện request list tiếp theo trả500 do unique index một operation chưa hoàn tất/profile.
- Đã thêm regression backend cho list sau Unknown: chứng kiến fail500 trước sửa. Đang kiểm tra guard dưới profile lock trả409 OPERATION_IN_PROGRESS, vẫn giữ reservation. Không xóa index hoặc thay schema/database nghiệp vụ.
- Kiểm thử này cũng xác nhận còn thiếu quy trình đối soát Unknown: restart Agent không tự giải quyết thao tác đã lấy khỏi queue. Đã hỏi người dùng về Admin-triggered reconciliation; chưa triển khai phần phụ thuộc quyết định đó.
- Review độc lập yêu cầu dọn cây process và che secret trong lỗi test; đã thêm private Windows Job Object và redaction, đang kiểm tra lại.
- Trạng thái CHƯA kết luận cuối: backend ProfileOperationTests và recovery E2E bản sửa đang chạy. Tiếp theo đọc log `docs/profile-live/backend-green.log`, `recovery-fixed.log`; hoàn thiện hướng dẫn demo, kiểm tra cleanup và review.

### 16/11/2026 — mốc giữa phiên: luồng Admin đối soát đã có, đang nghiệm thu cuối

- Người dùng đã chọn và tiếp tục xác nhận: thêm Admin yêu cầu Agent đối soát, có nhật ký, không tự xóa file.
- Thiết kế tại `docs/superpowers/plans/2026-11-15-admin-reconciliation.md`. Tái dùng CommandJson chỉ lưu metadata có version/attemptId/actorId/time; không thêm migration, không lưu nội dung tài liệu. Original operation giữ trạng thái Reconciled sau thành công, retry request gốc trả409 OPERATION_RECONCILED thay vì tự thực hiện lại.
- Đã thêm API Admin POST `/profiles/{id}/reconcile`, GET history; quyền Employee bị chặn; kết quả Agent có attemptId để loại response cũ. Quota chỉ được giải phóng khi Agent quét đủ vùng lưu trữ hợp lệ. Lỗi/timeout giữ reservation để Admin thử lại.
- QuotasPage có số giữ chỗ, trạng thái, nút Đối soát (xác nhận), Nhật ký. Agent scan chỉ metadata và kiểm tra binding, không đọc body/xóa file. Vùng lưu trữ thiếu hoặc không đọc được từ chối thành công.
- Backend6 ca trước review PASS. Review bắt thêm: failed reconciliation không được mở lại mutation gốc; os.walk không được bỏ qua PermissionError. Đã tạo regression fail chứng minh cả hai rồi sửa fence theo metadata và onerror của scan.
- Agent hiện **46 PASS, 1 SKIP**, gồm regression restore, reconcile, partial scan, guard database/cổng, Windows Job Object kill descendant. Full backend/frontend/build đang chạy; chưa kết luận hoàn tất tính năng.
- Live UI đã đi qua Admin reconcile thành công và giữ file/quota; test dừng ở selector text nhật ký trùng với Popconfirm. Đã sửa scope selector vào dialog, cần chạy lại để kết luận.
- Cần đọc `docs/profile-live/backend-final.log`, `frontend-tests.log`, `frontend-build.log`; chạy lại `agent/profile_e2e.py --suite all`, review lần cuối và cập nhật docs. Main API 5038 vẫn là binary cũ, cần restart bằng bản mới sau kiểm thử; không migrate/seed DB hiện có.


### 16/11/2026 — hoàn tất mốc Admin đối soát và bộ kiểm thử tái lập

- Hoàn thành phần người dùng đã chốt: Admin yêu cầu Agent đối soát, có nhật ký, không tự xóa file/không đọc body tài liệu. UI ở `/admin/quotas`, hiện pending/giữ chỗ, nút Đối soát và Nhật ký20 sự kiện mới nhất.
- Giữ một operation chưa hoàn tất/profile. CommandJson chỉ lưu metadata attempt có version, ID người yêu cầu và timestamp. Không thêm migration/schema; không migrate/seed ITManagement. API và Agent cần chạy cùng phiên bản mới.
- Kết quả hiện tại: **84 backend PASS; 46 Agent PASS,1 SKIP (symlink privilege); 29 frontend PASS; build frontend/API PASS; 26 live checks PASS**. Có lượt visual bổ sung recovery6/6, desktop/modal hiển thị rõ, viewport390 không tràn ngang document.
- Live suite cuối: `core-db3b4af532b9478cb732c70a9351ba36`(8), `files-2ba5f1c322c740d980c85849f7117be3`(12), `recovery-841175376b094a008b1c6904214cd791`(6), dưới artifacts/profile-e2e. Cả3 summary passed=true/cleanup=true. Visual mới nhất: `recovery-ef0420b1f8e0436784986ff66e16313c/reconciliation-admin.png` và `reconciliation-mobile.png` (đã xem).
- Đã tái hiện và sửa: restore sang folder/name khác đổi fileId; list khi Unknown trả500 duplicate index; đối soát thất bại phải tiếp tục chặn kết quả/mutation gốc; PermissionError trong scan không được coi là inventory đầy đủ.
- Đối soát thành công cập nhật metadata/count và giải phóng reservation, đánh dấu Reconciled. Request gốc trả409 OPERATION_RECONCILED; request mới sau khi người dùng xem danh sách có thể thực hiện. Lỗi/timeout vẫn giữ reservation. Attempt cũ hoặc normal result đến muộn không được ghi đè kết quả hiện tại.
- Review độc lập phát hiện lỗi và đã được sửa/kiểm tra lại; review cuối không còn finding cần sửa. Runner dùng private Windows Job Object, có test child cleanup, từ chối DB ngoài prefixGUID/cổng ứng dụng; test log che password/token/session.
- Source mới: `agent/profile_e2e.py`, `agent/tests/profile_fault_worker.py`, `agent/tests/test_profile_e2e.py`, `frontend/admin/tests/live/`(+README), `live-support.test.mjs`, `ProfileReconciliationService.cs`. Source sửa: Agent engine/transport/tests, API service/contracts/controllers/tests, QuotasPage. Tài liệu: RUNNING.md, agent/README.md, file tiến độ và .gitignore artifact.
- Đã restart đúng process API của project cổng 5038, build0 lỗi, đặt InitializeOnStartup=false. Đã kiểm tra API routes mới, UI `/admin/quotas?demo=0` và API machines qua proxy trả200. Frontend5173 vẫn chạy. Không tự enroll Agent hoặc tạo nhân viên/tài sản trong DB nghiệp vụ.
- Chạy lại: chuẩn bị Edge/Playwright/sqlcmd/LocalDB, dùng `.test-venv/Scripts/python.exe agent/profile_e2e.py --suite all`; tùy chọn core/files/recovery. Không chạy `.cjs` trực tiếp với credential nghiệp vụ. Xem README trong tests/live cho cách cài module và đọc artifact.
- Giới hạn: một máy vật lý/mode simulated; đã kiểm tra crash sau intent trước write, không tuyên bố mọi thời điểm crash đều được nghiệm thu. Không có Real inventory/Office desktop editor/full PDF visual fidelity. Agent gốc không sẵn sàng/mất index/root thì giữ lỗi, không tự tạo dữ liệu thay thế.
- Chưa xử lý: cảnh báo NuGet AutoMapper16.2.0; chunkJS >500kB; audit tổng hợp/ticket/hợp nhất storage cần chốt phạm vi. Không commit/push.
- Dừng tại mốc đã nghiệm thu để không mở dở bước lớn tiếp theo. Phiên sau đọc mục Điểm tiếp tục, kiểm tra git status và giữ toàn bộ thay đổi hiện có.

## Mốc 16/11/2026 — Hai cổng đăng nhập riêng

- Người dùng chốt `/admin`, nền navy #1B2A4A, thẻ trắng ở giữa. Giữ logo STU và bảng màu hiện có.
- URL gốc khi chưa có phiên tự vào `/employee`; nhân viên giữ bố cục riêng. Bỏ link Quản trị trên `/machines`, không thêm link Admin công khai.
- Form dùng chung xử lý lỗi/đang gửi; từ chối tài khoản sai cổng và xóa token vừa nhận. Employee/Technician dùng cổng nhân viên. API vẫn kiểm tra quyền ở backend.
- Giữ khôi phục phiên hiện hữu và deep link Admin hợp lệ; không sửa schema, dữ liệu hay backend.
- File: LoginPage.tsx, MachinesPage.tsx, routing.ts, styles.css, index.html, routing.test.mjs, tests/login-portals.cjs; RUNNING.md và .gitignore.
- Kiểm thử: 30/30 frontend; 9/9 UI Edge dùng API giả lập (mặc định, sai cổng hai chiều, nhân viên đăng nhập/đăng xuất, Admin deep link, màu nền/thẻ, mobile390, không link Admin). Đã xem ảnh desktop/mobile tại artifacts/login-portals. Build PASS; cảnh báo chunk >500kB còn như trước.
- UI smoke không thay thế kiểm thử xác thực SQL thật; backend không đổi, không chạy lại bộ SQL/Agent ở mốc này.
- Chạy UI smoke từ gốc repo: đặt PLAYWRIGHT_MODULE trỏ module Playwright, chạy `node frontend/admin/tests/login-portals.cjs`; UI_BASE_URL mặc định http://127.0.0.1:5173.
## Mốc 16/11/2026 — Dọn dependency AutoMapper

- Rà toàn bộ backend: AutoMapper chỉ có PackageReference và AddAutoMapper; không có IMapper, CreateMap, ProjectTo hoặc mapping profile nghiệp vụ.
- Bỏ package16.2.0 và đăng ký DI không dùng, không thay đổi schema/API contract/logic nghiệp vụ.
- Nguồn cảnh báo đã kiểm tra: https://github.com/advisories/GHSA-6vhm-j9h7-f9gh và https://github.com/advisories/GHSA-gpwp-5797-mjff. Không suy ra ứng dụng đã bị khai thác.
- `dotnet build backend/ITManagement.slnx --artifacts-path artifacts/backend-acceptance --no-restore`: PASS,0warning/0error.
- `dotnet list backend/ITManagement.slnx package --vulnerable --include-transitive`: cả5 project không phát hiện package có lỗ hổng theo nguồn NuGet hiện tại.
- Test FoundationTests và SqlServerModelTests:3/3PASS. Log trong docs/profile-live/{build-dependency,audit-dependency,test-dependency-offline}.log.
- Chặn kiểm thử SQL: sqlcmd không kết nối được pipe LocalDB đang ghi trong error.log; không thấy tiến trình sqlservr. Đã hỏi người dùng mở SSMS; không tự khởi động/cấu hình SQL.
- Chưa chạy lại84 test SQL sau thay đổi; số84 ở mốc trước là kết quả lịch sử. Chưa rebuild/restart API chính để tránh thay bản đang chạy khi kiểm thử tích hợp còn chờ.
- Tiếp tục: khi SSMS đã kết nối, đọc pipe mới trong error.log, đặt TEST_SQLSERVER, chạy dotnet test với artifacts/backend-acceptance; nếu đạt thì cập nhật API đang chạy, kiểm tra endpoint và ghi kết quả.
### Nghiệm thu tiếp sau khi người dùng mở SSMS — 16/11/2026

- Kết nối được LocalDB qua pipe mới; đọc error.log bằng FileShare.ReadWrite vì SQL đang giữ file. Không đổi cấu hình hay khởi động lại SQL.
- `dotnet test backend/ITManagement.slnx --artifacts-path artifacts/backend-acceptance --no-restore`:84/84PASS,0skip, thời gian test1m27s. Dùng TEST_SQLSERVER và database GUID riêng. Log: docs/profile-live/test-dependency-sql.log.
- API build bản chạy chính:0warning/0error; dependency runtime không còn AutoMapper. Log: docs/profile-live/build-api-dependency.log.
- Đã chạy API5038 và Vite5173 ẩn; Database__InitializeOnStartup=false, không migrate/seed DB nghiệp vụ. API dùng pipe hiện tại cho phiên chạy này; pipe thay đổi nếu LocalDB khởi động lại.
- Kiểm tra HTTP200: Swagger, /api/machines qua Vite, /employee, /admin. Log API: artifacts/api-dependency-20261116.{out,err}.log.
- Phần chờ SQL ở mốc trước đã giải quyết. Chưa triển khai trang nhật ký/metadata Admin tổng hợp vì còn cần chốt phạm vi.
## 17/11/2026 — Tiếp tục chuyển mô phỏng web (đang thực hiện)

- User đã chốt: chọn máy trước login; mọi Employee active dùng mọi máy active; giữ thao tác file, lưu trên server. Không yêu cầu Agent/enrollment/grant cho luồng này.
- Code hiện có: Simulation:UseAgent=false mặc định; legacy Agent giữ với cấu hình true. ServerProfileOperations khóa SQL và ghi blob bất biến, metadata+quota+idempotency trong transaction. ServerProfileStorage chỉ dùng GUID làm đường dẫn.
- Migration additive 20261116145149_ServerSimulation: StorageBackend default Agent để giữ profile cũ, StorageKey nullable và machine_specifications. Snapshot SQL đã cập nhật đúng; EF tooling từng ghi nhầm snapshot vào thư mục migrations cũ và đã khôi phục.
- Profile Agent có dữ liệu không bị tự đổi hoặc xóa; cần chuyển riêng nếu gặp. UI có thông tin/phần mềm/file, Admin có cấu hình mô phỏng; Agent ẩn khỏi menu chính.
- docs/profile-live/server-full.log:96/96 backendPASS. Đang bổ sung regression UTC sau reviewer phát hiện LastRestartedAt SQL datetime2 mất timezone; chưa kết luận bản cuối.
- Còn làm: sửa UTC, test liên quan; E2E thật không Agent + restartAPI; kiểm tra dữ liệu hiện có, backup DB rồi migrate additive nếu an toàn; build/chạy app và cập nhật hướng dẫn.
## 17/11/2026 — HOÀN TẤT mô phỏng web không Agent

### Quyết định mới thay thế các mô tả Agent/grant cũ
- Mặc định chọn máy trước login. Employee active dùng mọi máy Desktop/Laptop/Server Available hoặc Assigned; không cần grant riêng/token/enrollment.
- Employee xem readonly tổng quan/chi tiết/phần mềm theo ảnh; giữ file Desktop/Documents/RecycleBin. Admin nhập cấu hình/phần mềm ở Tài sản → Cấu hình mô phỏng. Giá trị chưa có hiển thị Chưa khai báo.
- Nội dung file nằm trên server, metadata/quota/idempotency ở SQL; server không tự thu thập inventory. Python chỉ còn là phần mở rộng legacy và công cụ chạy test, không cần chạy Agent cho luồng mới.
- User chọn GIỮ file Agent cũ, chuyển sau. Mỗi người–máy có profile Server riêng và Agent riêng; index mới không cho trùng cùng backend. File cũ không tự xuất hiện ở vùng server. Khi chuyển mode, phiên thuộc backend cũ không giữ chỗ máy ở luồng mới.

### Đã triển khai và kiểm tra
- ServerProfileOperations/Storage: khóa asset→user→profile, quota atomic, idempotency, blob bất biến có flush trước SQL commit, GUID paths ngoài webroot, không dùng tên user nhập làm đường dẫn; giữ orphan/phiên bản cũ, chưa có auto garbage collection.
- Chặn sai user/máy/phiên, tài khoản khóa/máy disabled, traversal/đuôi file cấm, quota vượt, metadata sai/mất nội dung. File tối đa8MiB; copy giữ blob bất biến, write tạo blob mới. Quota file không phải quota byte của ổ đĩa mô phỏng.
- Sửa UTC LastRestartedAt/UpdatedAt từ SQL datetime2 và normalize ngày khi lưu; regression assert Z. UI nền trắng giúp cả ba tab hiển thị đủ tương phản; responsive390px đã xem ảnh.
- Backend96/96PASS sau thay đổi cuối: docs/profile-live/server-final.log. 17 regression liên quan cũng đạt trước full run (server-profile-green.log).
- Frontend32/32PASS; buildPASS (còn warning chunk >500kB). Browser mock thông tin/Admin editor/legacy quota đạt: docs/profile-live/server-ui-smoke.log.
- E2E không Agent9/9PASS, cleanup=true: artifacts/server-simulation-e2e/ad7dbb4294154d76828a4ef5b1462c4f. Chọn máy/login, readonly info/software, tạo/sửa TXT, PDF/DOCX upload + tảiDOCX đúng byte, F5, restartAPI, đổi máy cách ly file, mobile/logout, kiểm tra blob thật. Database GUID riêng đã xóa đúng guard, test ports5189/5190 đã dừng.
- Runner mới: backend/tests/server_simulation_e2e.py và frontend/admin/tests/live/server-simulation.cjs. Chỉ chạy suite hiện hành này để nghiệm thu web mới; bộ profile_e2e Agent26 ca cũ là bằng chứng lịch sử, không phải nghiệm thu UI mới.

### Dữ liệu và bản đang chạy
- Backup COPY_ONLY/CHECKSUM + RESTORE VERIFYONLY hợp lệ trước mỗi migration. Bản trước thay đổi: artifacts/backups/ITManagement-before-server-simulation-620bfca8d16949cda8f9edefe081806c.bak; bản trước tách profile: artifacts/backups/ITManagement-before-separate-profiles-fd9e3d86265643b39afa036cb8efd416.bak.
- Đã áp dụng20261116145149_ServerSimulation (cột/bảng additive) và20261117052223_SeparateServerAndAgentProfiles (đổi index unique sang UserId/AssetId/StorageBackend). Không seed/reset password. Sau migration vẫn5assets,4Agentprofiles,1file cũ.
- API chính build0warning/0error, chạy5038; Vite5173. Simulation__UseAgent=false, Database__InitializeOnStartup=false; root cố định D:\DoAnChuyenNganhT10_2026\backend\src\ITManagement.API\storage\simulation. SQL dùng pipe hiện hành, cần cập nhật khi instance restart.
- API machines qua proxy200, Swagger có information/specification. Logs artifacts/api-server-simulation.{out,err}.log. Không đổi/xóa SQL instance.
- Đã cập nhật README/RUNNING, ignore kho file và backup/artifacts. Không commit/push trong lượt17/11; thay đổi16/11 đã nằm trong HEAD khi tiếp tục, giữ nguyên.
- EF CLI hay ghi snapshot nhầm vào Persistence/Migrations (legacy excluded) do trùng tên file: sau generate đã chép snapshot mới về SqlServerMigrations và khôi phục nguyên file legacy. Model snapshot test đạt; tránh tạo migration trùng lặp.
- Còn lại: nhật ký/metadata Admin tổng hợp, Ticket, chuyển file cũ theo yêu cầu sau; không coi toàn bộ đồ án đã hoàn tất chỉ vì luồng này đạt.
### 17/11/2026 — Dọn tài liệu và bàn giao khi gần giới hạn
- Tách lịch sử nghiệm thu/hướng dẫn Agent khỏi RUNNING.md sang legacy/README.md; đánh dấu Agent tùy chọn/legacy ngay đầu agent/README.md.
- Hướng dẫn hiện tại làm rõ runtime chỉ cần .NET/Node/SQL; Python cho test/legacy, demo trình duyệt không chứng minh file lưu thật.
- Cập nhật mục5–6 thành backlog và kế hoạch theo mốc nhỏ. Nhật ký/metadata đã được user đồng ý, không hỏi lại phạm vi này.
- Chỉ sửa tài liệu. Không đổi API, schema, file người dùng, backup hoặc tiến trình đang chạy. Không chạy lại96 test vì không đổi code.
- Cache17 file tổng183172bytes còn nguyên: automatic approval review từ chối lệnh Remove-Item với thông báo blocked by policy; không được cung cấp lý do chi tiết hơn.
