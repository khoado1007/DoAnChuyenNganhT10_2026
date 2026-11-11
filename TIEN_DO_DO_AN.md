# Tiến độ và bàn giao đồ án IT Management

**Cập nhật gần nhất:** 11/11/2026 — giờ Việt Nam.  
**Workspace đúng:** `D:\DoAnChuyenNganhT10_2026` (đường dẫn cũ `D:\DoAnChuyenNganh` không tồn tại).  
**Điểm tiếp tục:** Phần chuyển đổi SQL Server LocalDB đã hoàn thiện và qua test; phần Python Agent và luồng nhân viên còn cần xử lý.

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
- Frontend hiện là một React SPA tại `frontend/admin`; folder `frontend/employee` trống. Chưa có quyết định tách thành hai ứng dụng.
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
| Frontend `npm test` khi rà soát | **20/20 đạt** | Kết quả trước thay đổi database; frontend không bị sửa logic trong đợt chuyển đổi |
| Frontend `npm run build` khi rà soát | Thành công | Có cảnh báo bundle lớn |
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
| 1 | Python Agent lưu state/file trên Windows | Đã sửa nguyên nhân ACL dùng username thiếu domain; bộ test hiện tại 33 đạt, 1 bỏ qua |
| 2 | Hoàn thiện luồng nhân viên | Cổng `/employee` chưa có lối vào màn hình chọn máy; không tự tách ứng dụng chỉ vì folder employee trống |
| 2 | Khôi phục/đóng phiên Desktop khi tải lại trang | Phiên nằm trong React state, backend vẫn giữ phiên; cần tái hiện trước khi sửa |
| 2 | Nhật ký tổng hợp và metadata file cho Admin | Có audit trong backend nhưng chưa có màn hình tra cứu đầy đủ |
| 3 | Ticket cơ bản | Có model/repository, UI đang thông báo chưa triển khai; chưa có luồng gửi và xử lý hoàn chỉnh |
| 3 | Thống nhất phần storage cũ và mới | API quota byte/lưu file server còn song song với profile Agent/quota số file; cần trao đổi trước khi loại bỏ hoặc thay đổi |
| 3 | Nghiệm thu end-to-end và tài liệu | Chưa chạy toàn bộ kịch bản trình duyệt + Real/Simulated Agent sau chuyển đổi |

Một số skill tham chiếu các tài liệu chưa tìm thấy (`STORAGE_POLICY.md`, `DEMO_ACCEPTANCE.md`, v.v.). Không giả định nội dung các file này hoặc xem skill là bằng chứng chức năng đã hoàn thành.

## 6. Bước tiếp theo cụ thể

1. Tiếp tục hoàn thiện luồng nhân viên: xác định cách vào `/machines` từ `/employee` trong SPA hiện tại; không tự tách ứng dụng.
2. Sau đó mới xử lý khôi phục/đóng phiên Desktop khi tải lại trang; tái hiện hành vi trước khi sửa.
3. Giữ nguyên thay đổi SQL Server và các thay đổi chưa commit khác; không đụng database nghiệp vụ.

## 7. Trạng thái bàn giao hiện tại

- Không có tác vụ triển khai đang chạy dở ở thời điểm ghi file.
- Thay đổi chuyển SQL Server đang nằm trong workspace, **chưa commit/push**.
- Không có thay đổi cài đặt SQL Server, không chạy migration trên database nghiệp vụ.
- Đã sửa Python Agent: `agent/itam_agent.py`, `agent/local_profiles.py`; chưa sửa giao diện nhân viên, ticket hoặc storage cũ.
- Agent tests trên Windows: **33 PASS, 1 SKIP** (`.\.test-venv\Scripts\python.exe -m pytest agent\tests -q`).
- Yêu cầu vừa thực hiện: tạo file bàn giao và chốt cập nhật theo giới hạn phiên, không cập nhật theo lịch hằng ngày.

## 8. Nhật ký

### 11/11/2026

- Rà soát tổng thể và xác nhận folder employee trống nhưng UI nhân viên nằm trong SPA hiện có.
- Chốt LocalDB và bỏ Docker khỏi quy trình chạy.
- Hoàn thiện chuyển đổi backend/database, kiểm tra 82/82 test đạt.
- Tái hiện lỗi lưu Agent trên Windows: `getpass.getuser()` chỉ trả username ngắn nên `icacls` tạo ACL sai; thay bằng account đầy đủ từ `whoami` cho state và thư mục nội bộ profile. Giữ ACL chỉ cấp cho đúng tài khoản, không nới quyền.
- Kiểm tra lại Agent: **33 PASS, 1 SKIP**; `git diff --check -- agent/itam_agent.py agent/local_profiles.py` PASS.
- Bước tiếp theo: hoàn thiện lối vào màn hình chọn máy từ `/employee`.
- Tạo file tiến độ này để phiên sau tiếp tục từ phần Python Agent.
