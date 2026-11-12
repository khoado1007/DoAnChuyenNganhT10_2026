# Python Agent: đăng ký và heartbeat

> **Hướng triển khai hiện tại — 06/11/2026:** xem [hướng dẫn chạy Windows/LocalDB](../RUNNING.md). Agent lưu nội dung bền vững trên ổ local; server quản lý quyền/quota và metadata đo lường. Các mô tả heartbeat/connectivity bên dưới thuộc baseline cũ, không phải yêu cầu làm dashboard online/offline. Luồng file mới chưa được nghiệm thu end-to-end; không suy ra hoàn tất từ lệnh CLI.


Một codebase cho `real` và `simulated`, mỗi instance dùng một file state riêng. `real` chỉ gửi hostname, OS, CPU, tổng RAM và phiên bản Agent khi truyền `--consent-inventory`. `simulated` đọc fixture và server giữ nhãn chế độ từ token, không cho heartbeat tự đổi nhãn. Không tạo tài khoản OS, không chạy lệnh từ xa, không đọc tài liệu cá nhân.

## Cài đặt và chạy

Các lệnh dưới đây chạy từ thư mục gốc repository. Cần Python 3.11+ và API/SQL Server LocalDB đang hoạt động.

```powershell
python -m venv agent\.venv
agent\.venv\Scripts\python.exe -m pip install -r agent\requirements-dev.txt
```

Đăng nhập Admin trên web, mở **Agent**, tạo/chọn tài sản và cấp token đúng chế độ. Chạy lệnh sau, dán token vào lời nhắc ẩn; không truyền token qua command line:

```powershell
agent\.venv\Scripts\python.exe agent\itam_agent.py enroll --server http://127.0.0.1:5038 --mode real --state agent\state\real.json --consent-inventory
agent\.venv\Scripts\python.exe agent\itam_agent.py run --server http://127.0.0.1:5038 --mode real --state agent\state\real.json --consent-inventory
```

Đối với mô phỏng, cấp token đúng chế độ rồi chạy:

```powershell
agent\.venv\Scripts\python.exe agent\itam_agent.py enroll --server http://127.0.0.1:5038 --mode simulated --state agent\state\simulated.json
```

Chế độ này chỉ gửi fixture giả lập nên không cần `--consent-inventory`; vẫn dùng token một lần tại lời nhắc ẩn. Dùng `--mode simulated --state agent\state\simulated.json` nhất quán cho các lệnh tiếp theo. Hai instance trên một PC không chứng minh hai thiết bị vật lý. Có thể dùng `once` thay `run` để gửi một heartbeat. Ctrl+C dừng nhẹ nhàng.

HTTP chỉ chấp nhận localhost/loopback; môi trường khác bắt buộc HTTPS có chứng chỉ hợp lệ. Không tự theo redirect. Request timeout 15 giây; heartbeat mặc định 30 giây, tùy chọn 10–60 giây; lỗi mạng/408/429/5xx dùng backoff+jitter tối đa 300 giây. 401/403/410 dừng và lưu trạng thái bị thu hồi, không tự đăng ký lại. Các lỗi giao thức 4xx khác cũng dừng.

State chứa credential riêng của Agent; không chia sẻ/commit. File được ghi atomic, POSIX 0600 hoặc Windows ACL chỉ user hiện tại. Không dùng chung file state giữa instance, server hoặc chế độ. File `.lock` có thể còn trên đĩa nhưng khóa hệ điều hành tự nhả khi process chết. Không xóa state để sửa lỗi mạng: sequence và heartbeat chờ gửi cần giữ qua restart.

## Hợp đồng API v1

Mọi response dùng envelope `success/message/data/errors`. OpenAPI tại `/swagger` khi API chạy Development.

| Endpoint | Quyền / nội dung |
| --- | --- |
| GET/POST `/api/assets` | Admin; tạo tài sản Desktop tối thiểu bằng `assetCode`, `name` |
| POST `/api/agents/enrollment-tokens` | Admin; `{assetId, mode}`; trả token một lần, hết hạn 15 phút; cấp mới hủy token cũ chưa dùng |
| POST `/api/agents/enroll` | `{token, instanceId: UUID, mode, protocolVersion: 1}`; trả `agentId`, `credential`, interval |
| POST `/api/agents/heartbeat` | Header `Authorization: Agent <credential>`; sequence tăng, protocolVersion=1, hostname, os, cpu, ramBytes, agentVersion |
| GET `/api/agents` | Admin; mode, policy, connectivity, assetStatus, lastSeenAt và inventory |
| POST `/api/agents/{id}/disable` | Admin; từ chối heartbeat sau khi disable hoàn tất, giữ snapshot trước đó |

Token/credential trong database chỉ là SHA-256 của secret ngẫu nhiên 256 bit. Agent credential không đăng nhập được như người dùng. Mỗi tài sản chỉ có một Agent chưa revoked; các Agent cũ được giữ làm lịch sử, mỗi instance ID là duy nhất. Token được tiêu thụ trong transaction cùng lúc tạo Agent. Enrollment không retry tự động: nếu server đã hoàn tất nhưng response/ghi state thất bại, cần quản trị xử lý cấp lại; dùng luồng Phục hồi mô tả bên dưới.

Server dùng UTC; sequence trùng/cũ không cập nhật last-seen hoặc inventory. Online khi heartbeat mới nhất cách hiện tại <90 giây. Offline không tự đổi tài sản sang retired; disabled là chính sách độc lập, vì vậy Agent vừa bị disable có thể còn hiển thị Online đến hết ngưỡng. Snapshot inventory gần nhất được giữ, chưa có bảng lịch sử từng heartbeat. Giới hạn theo IP: 20 enrollment/phút, 120 heartbeat/phút; cụm nhiều Agent sau cùng NAT cần điều chỉnh trước triển khai lớn.

## Kiểm thử

```powershell
# SQL Server LocalDB: test tự tạo database riêng, không dùng database nghiệp vụ.
dotnet test .\backend\ITManagement.slnx
agent\.venv\Scripts\python.exe -m pytest .\agent\tests -q
npm --prefix frontend/admin test
npm --prefix frontend/admin run build
```

Live smoke: khởi chạy API Development trên `127.0.0.1:5189`, trỏ `ConnectionStrings__DefaultConnection` tới database `itam_agent_e2e` trên SQL Server LocalDB riêng. Đặt `ITAM_TEST_PASSWORD` bằng mật khẩu admin test rồi chạy:

```powershell
agent\.venv\Scripts\python.exe agent\e2e_smoke.py --server http://127.0.0.1:5189 --consent-inventory
```

UI smoke: Vite cổng 5190 với `API_PROXY_TARGET=http://127.0.0.1:5189`, cần Playwright và Edge. Đặt `PLAYWRIGHT_MODULE` tới module Playwright nếu không có trong node_modules; chạy `node .\frontend\admin\tests\agents\smoke.cjs`. Có thể đặt `ITAM_UI_URL` và `ITAM_SCREENSHOT`. Smoke test tạo bản ghi test thật, chỉ chạy trên database dùng thử. Các bài kiểm thử allocation/files/quota/tickets không thuộc thay đổi này.

## Phục hồi sau khi mất state hoặc response đăng ký

Admin mở Agent → **Cấp lại / Phục hồi**, chọn đúng Real/Simulated và nhập lý do. POST `/api/agents/recovery-tokens` nhận `{assetId, mode, reason}`. Ngay khi cấp token, identity cũ bị thu hồi vĩnh viễn, credential cũ bị từ chối; bản ghi, lý do và snapshot cũ vẫn giữ. Token mới có hạn 15 phút và dùng một lần; cấp lại token sẽ hủy token chưa dùng trước đó.

Dừng instance cũ. Nếu còn state, chạy từ thư mục gốc repository:

```powershell
agent\.venv\Scripts\python.exe agent\itam_agent.py re-enroll --server http://127.0.0.1:5038 --mode simulated --state agent\state\simulated.json
```

Dùng đúng đường dẫn state của instance và đúng mode đã chọn trên UI; Real cần thêm `--consent-inventory`. Token nhập tại lời nhắc, không truyền trên command line. `re-enroll` tạo instance ID mới và xóa credential/pending/sequence cũ trước khi gửi yêu cầu. Nếu state mất hoàn toàn, dùng `enroll` với đường dẫn state mới. Không chạy hai instance trên cùng state.

Nếu response bị mất hoặc không ghi được state sau khi máy chủ đã đăng ký: không tự retry token cũ; Admin cấp token recovery mới rồi thực hiện lại. Token sai/hết hạn cũng cần cấp lại. Tái đăng ký không thay đổi chủ sở hữu tài sản hoặc dữ liệu tài khoản. Migration recovery giữ nhiều Agent lịch sử trên một tài sản, chỉ một Agent chưa revoked; không downgrade migration về unique AssetId khi đã có lịch sử thay thế mà chưa lập kế hoạch chuyển dữ liệu.

## Local profile file operations

Sau enrollment, dùng credential state hiện có để chạy file Agent; chế độ này không gửi heartbeat hoặc thu thập inventory liên tục:

```powershell
agent\.venv\Scripts\python.exe agent\itam_agent.py serve-files --server http://127.0.0.1:5038 --mode simulated --state agent\state\simulated.json --root D:\ITManagementSim
```

Real Agent dùng `--mode real` và state riêng. Storage được tách thành `<root>/<mode>/Devices/<deviceUuid>/Profiles/<userUuid>/Desktop|Documents`; `System/<userUuid>` ngoài profile giữ index, metadata journal, staging và Recycle. Không dùng Desktop/Documents Windows thật, không chạy shell từ xa. Thư mục System chỉ cấp quyền cho Windows account đang chạy Agent; các danh tính mô phỏng cùng Windows account vẫn không có cách ly hệ điều hành.

Agent poll `/api/agents/operations/poll`, recheck `/api/agents/operations/{id}/authorize`, rồi gửi `/api/agents/operations/{id}/result`. Khi gửi result gặp lỗi tạm thời, giữ result trong RAM và thử lại trước khi nhận thao tác mới. Journal chỉ chứa metadata; không lưu body response. Restart replay kết quả mutation đã xác nhận; intent bị gián đoạn hoặc read chưa được ACK trả `FILE_OPERATION_UNKNOWN`, cần reconcile, không tự chạy lại mutation. `recoveryOnly` không được tạo/ghi nội dung mới khi API đã mất ephemeral payload.

Quota đếm mọi regular file, kể cả nested/hidden/desktop.ini; thư mục và Recycle ngoài profile không tính. Tạo/copy/restore kiểm tra quota, edit/rename/move không tăng số file. Delete chuyển vào Recycle riêng, không purge. Binary import tối đa 8 MiB; chỉ `.txt`/`.csv` cho phép sửa. Giới hạn dung lượng trống kỹ thuật 16 MiB độc lập quota. Profile scan tối đa 10.000 mục để tránh duyệt vô hạn. Agent từ chối traversal, ADS, reserved names, symlink/junction/reparse point và hardlink; người có quyền ghi trực tiếp bằng cùng OS account vẫn có thể sửa dữ liệu ngoài ứng dụng.
