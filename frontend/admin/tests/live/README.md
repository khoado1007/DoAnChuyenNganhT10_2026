# Kiểm thử live profile trên Windows

Bộ chạy dùng HTTP API, SQL Server LocalDB, Edge và Python Agent thật ở mode `simulated`. Không mock API; không đọc inventory máy, không dùng database `ITManagement` hoặc Desktop/Documents Windows.

## Chuẩn bị

Từ thư mục gốc đồ án:

- .NET SDK hỗ trợ net10.0, Node.js, Python 3.11+, Microsoft Edge và `sqlcmd` trên PATH.
- LocalDB đã chạy. Có thể mở SSMS kết nối `(localdb)\MSSQLLocalDB`. Runner đọc named pipe hiện tại từ error.log và kiểm tra kết nối; không tự cài/reset/start SQL Server.
- `npm --prefix frontend/admin ci` và Python environment theo [hướng dẫn Agent](../../../../agent/README.md).
- Playwright có sẵn trong `frontend/admin/node_modules`, hoặc đặt `PLAYWRIGHT_MODULE` tới thư mục module đã cài. Bản kiểm tra ở workspace dùng Playwright 1.62.1 và Edge cài trên máy. Không cần tải Chromium khi dùng Edge.

Nếu chưa có module Playwright, có thể cài riêng cho kiểm thử, không sửa package/lockfile của ứng dụng:

```powershell
npm --prefix frontend/admin install --no-save --package-lock=false playwright@1.62.1
```

Nếu đã cài ở nơi khác:

```powershell
$env:PLAYWRIGHT_MODULE = 'C:\tools\playwright\node_modules\playwright' # thay bằng vị trí thật
```

## Chạy

```powershell
.\.test-venv\Scripts\python.exe agent\profile_e2e.py --suite all
```

Hoặc dùng `agent\.venv\Scripts\python.exe` nếu đó là environment đã cài requirements. Runner tự build API sang `artifacts/profile-e2e/api-build`; không thay binary API đang chạy cổng 5038.

- `--suite core`: portal, đăng nhập máy, file, F5, quota cạnh tranh, phân quyền và logout.
- `--suite files`: rename/copy/move/delete/restore, CSV/PDF/DOCX import, byte fidelity, download DOCX, mở iframe PDF, restart Agent/API, giữ fileId.
- `--suite recovery`: ngắt Agent sau intent journal nhưng trước khi ghi file; xác minh trạng thái Unknown/reservation, sau đó Admin dùng màn hình Quota để yêu cầu đối soát và xem nhật ký. File cũ giữ nguyên, lệnh gốc không tự chạy lại, thao tác mới dùng quota đã cập nhật.
- `--no-build`: chỉ dùng khi đã build bằng runner và backend không thay đổi.
- `--api-port 5189 --ui-port 5190`: cổng test mặc định. Chọn cổng khác nếu bận; runner không dùng 5038/5173 và không dừng dịch vụ có sẵn.

Mỗi suite tạo database `itam_profile_e2e_<GUID>` cùng thư mục artifact riêng. Password/token chỉ dùng tạm; không in ra log. Chỉ đúng database do runner tạo được xóa khi kết thúc. Windows Job riêng dừng cây process của runner khi thoát, kể cả browser descendants. Nếu hệ thống bị mất điện/kill cứng trước cleanup database, đối chiếu tên DB trong `summary.json`/log trước khi xử lý; không xóa theo wildcard hoặc đụng `ITManagement`.

## Kết quả và giới hạn

Xem `artifacts/profile-e2e/<suite>-<GUID>/summary.json`, `browser.log`, `api.log`, log Agent và ảnh. Summary ghi riêng kết quả kiểm thử và cleanup. Artifact bị Git bỏ qua, có state credential test; không chia sẻ nguyên thư mục state. Bộ chạy không để Agent test gắn vào dữ liệu nghiệp vụ sau khi kết thúc.

Các lỗi startup, cổng bận, thiếu dependency hoặc test fail trả exit code khác 0. Không chạy trực tiếp các file `.cjs` với thông tin đăng nhập thật.

Chứng minh một host, một Agent mode simulated và các tình huống được liệt kê. Không chứng minh hai thiết bị vật lý, Real inventory, chất lượng hiển thị toàn bộ tài liệu Office/PDF hoặc mọi thời điểm crash có thể xảy ra.
