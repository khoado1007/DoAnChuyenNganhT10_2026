# IT Management — Quản lý tài sản CNTT và mô phỏng máy công ty

IT Management là đồ án quản lý máy dùng chung cho doanh nghiệp. Admin ghi nhận người chịu trách nhiệm về tài sản, khai báo cấu hình và phần mềm. Nhân viên chọn máy trên trình duyệt, đăng nhập bằng tài khoản được kích hoạt và làm việc trong Desktop/Documents riêng.

**Bàn giao máy và quyền đăng nhập là hai nghiệp vụ riêng.** Bàn giao xác định ai chịu trách nhiệm về tài sản. Mọi Employee đang hoạt động đều có thể chọn máy khả dụng. Ví dụ, hai nhân viên lần lượt đăng nhập cùng một máy sẽ thấy hai bộ tài liệu khác nhau. Khi một người quay lại đúng máy, hệ thống mở lại hồ sơ của người đó.

## Chạy trên Windows bằng Git Bash

Cần .NET SDK hỗ trợ `net10.0`, Node.js và npm, SQL Server Express LocalDB với Windows Authentication. Dùng Node.js 22.18 trở lên để chạy cả bộ kiểm thử frontend có import TypeScript. Luồng web chạy trực tiếp trên Windows, không yêu cầu Docker hay Python Agent.

Mở **hai terminal tại thư mục gốc repository**. Các lệnh dưới đây dành cho database `ITManagement` đã khởi tạo trên instance `(localdb)\MSSQLLocalDB`. Nếu chạy trên máy mới, xem phần **API và database hiện có** trong [hướng dẫn khởi tạo LocalDB và Admin](RUNNING.md). API cần khóa ký JWT ít nhất 32 byte. Nếu môi trường chưa cấu hình khóa riêng, đặt biến `Jwt__Secret` trong terminal chạy API.

**Terminal 1 — API**

```bash
dotnet restore backend/ITManagement.slnx
dotnet run --project backend/src/ITManagement.API --launch-profile http
```

**Terminal 2 — giao diện web**

```bash
npm --prefix frontend/admin ci
npm --prefix frontend/admin run dev -- --host 127.0.0.1
```

Giữ cả hai terminal mở trong lúc sử dụng. Lệnh `npm ci` chỉ cần chạy khi cài lần đầu hoặc khi lockfile thay đổi. Vite chuyển yêu cầu `/api` từ cổng 5173 sang API ở cổng 5038.

| Điểm vào | Cách sử dụng |
| --- | --- |
| [Employee](http://127.0.0.1:5173/employee?demo=0) | Cổng mặc định khi mở website. Chọn máy rồi đăng nhập. |
| [Admin](http://127.0.0.1:5173/admin?demo=0) | Mở trực tiếp đường dẫn này và dùng tài khoản quản trị của database. Quyền Admin được kiểm tra ở API. |
| [Admin demo](http://127.0.0.1:5173/admin?demo=1) | Xem thử giao diện với `admin` / `Demo@12345`. Dữ liệu demo nằm trong bộ nhớ trình duyệt và được tạo lại khi tải lại trang. |
| [Swagger](http://localhost:5038/swagger) | Tra cứu và thử các API khi chạy với cấu hình Development. |

**Nếu không đăng nhập được:** mở [danh sách máy qua web](http://127.0.0.1:5173/api/machines). Nếu nhận JSON thành công, web đã gọi được API và API đã đọc được database. Kiểm tra đúng cổng đăng nhập, chế độ `demo=0` và tài khoản đang hoạt động. Nếu yêu cầu thất bại, kiểm tra terminal API và kết nối LocalDB. Tài khoản demo chỉ dùng với `demo=1`.

## Nghiệp vụ đã triển khai

| Chức năng | Hành vi |
| --- | --- |
| Tài sản và bàn giao | Admin khai báo máy, theo dõi người nhận, trả máy và xem lịch sử. Desktop, Laptop và Server ở trạng thái sẵn sàng hoặc đã bàn giao có thể xuất hiện trong màn hình chọn máy. |
| Phiên máy và hồ sơ | Mỗi máy chỉ có một phiên chưa kết thúc. Đăng nhập một lần để mở ngay Desktop/Documents. Đăng xuất trở về trang đăng nhập của máy đã chọn. F5 mở lại đúng Desktop nếu phiên và quyền còn hợp lệ. Khi nhận tín hiệu rời trang, máy chủ chờ 60 giây cho tab quay lại rồi giải phóng máy. Chuyển tab hoặc ngừng thao tác vẫn giữ phiên trong hạn bình thường. Nhân viên có hồ sơ và quota riêng trên từng máy. |
| Tài khoản Employee | Admin tạo, khóa, mở lại hoặc reset mật khẩu. Employee đổi mật khẩu tại **Thông tin tài khoản** sau khi đăng nhập máy. |
| Tài liệu và quota | Mặc định 10 file cho mỗi hồ sơ. Tạo, sao chép và khôi phục cần suất trống. Đổi tên, sửa và di chuyển giữ nguyên số file. Xóa đưa tài liệu vào thùng rác. Hỗ trợ sửa TXT/CSV, nhập tài liệu Office/PDF tối đa 8 MiB mỗi file. |
| Nhật ký và metadata | Admin xem bảy thao tác file: tạo, sửa, đổi tên, di chuyển, sao chép, xóa và khôi phục. Trang metadata hiển thị tên, phần mở rộng, kích thước, chủ sở hữu và máy của file hiện tại. Hai trang dành cho việc tra cứu và không cung cấp nội dung tài liệu. |
| Yêu cầu hỗ trợ | Employee gửi và theo dõi ticket của mình. Admin tiếp nhận, trao đổi, ghi chú nội bộ và chuyển trạng thái từ mở → đang xử lý → đã xử lý → đóng. |

**Chu kỳ mật khẩu.** Sau khi tạo hoặc reset tài khoản Employee bằng mật khẩu mặc định, thời hạn bảy ngày bắt đầu ở lần đăng nhập đầu tiên. Employee có thể đổi ngay hoặc bỏ qua. Hệ thống nhắc lại vào ngày 5, 6 và 7. Khi đủ bảy ngày mà mật khẩu chưa đổi, tài khoản bị khóa. Nếu Admin mở lại tài khoản bị khóa vì quá hạn, mật khẩu hiện tại được giữ và chu kỳ mới bắt đầu ở lần đăng nhập kế tiếp.

### Quản lý App/Service

Trong **Admin → Tài sản → Cấu hình mô phỏng**, Admin khai báo từng ứng dụng hoặc dịch vụ cho một máy. Để áp dụng cùng danh sách cho nhiều máy, chọn **Thêm phần mềm hàng loạt**. Mỗi lượt cho phép chọn tối đa 50 máy và mỗi máy chứa tối đa 200 mục. Nếu tên và phân loại đã tồn tại trên một máy, hệ thống giữ nguyên phiên bản và đường dẫn của mục đó, rồi báo số mục đã thêm và số mục bỏ qua cho từng máy.

Employee xem danh sách trong tab **Phần mềm**. Đường dẫn cài đặt chỉ hiển thị cho Admin. Khi Employee chọn **Yêu cầu xóa**, Admin xác nhận ngay trên máy cho đúng mục cần xóa. Phiên Employee được giữ nguyên. Các mục App/Service thuộc cấu hình mô phỏng, không tính vào quota và không ghi vào lịch sử file.

## Kiến trúc và lưu trữ

| Thành phần | Vị trí | Trách nhiệm |
| --- | --- | --- |
| React, TypeScript, Vite, Ant Design | [frontend/admin](frontend/admin/) | Một ứng dụng web phục vụ cả cổng Admin và Employee. |
| ASP.NET Core Web API, Identity, EF Core | [backend/src](backend/src/) | Xác thực, phân quyền, quản lý phiên và thực hiện nghiệp vụ. |
| SQL Server LocalDB | Database `ITManagement` | Lưu tài khoản, tài sản, cấu hình, phiên, metadata, quota, lịch sử và ticket. |
| Kho tài liệu trên máy chủ | `backend/src/ITManagement.API/storage/simulation` | Lưu nội dung file theo định danh máy, người dùng và hồ sơ. Có thể cấu hình bằng `Simulation__StorageRoot`. |
| Python Agent cũ | [agent](agent/README.md) | Giữ tương thích với hồ sơ lưu trữ cũ. Luồng web hiện tại sử dụng `Simulation__UseAgent=false`. |

Khi sao lưu, cần giữ **cả database lẫn kho tài liệu** để phục hồi được tên file, quyền sở hữu và nội dung. Desktop/Documents là thư mục mô phỏng trong ứng dụng. Việc đăng nhập không tạo tài khoản hoặc thay đổi màn hình đăng nhập Windows.

## Kiểm thử và nghiệm thu

Từ thư mục gốc, chạy:

```bash
dotnet test backend/ITManagement.slnx
npm --prefix frontend/admin test
npm --prefix frontend/admin run build
```

Bộ kiểm thử tích hợp tạo database SQL Server riêng cho dữ liệu thử. Hướng dẫn chạy các kịch bản trình duyệt và cấu hình môi trường kiểm thử nằm trong [RUNNING.md](RUNNING.md).

| Mốc nghiệm thu ngày 25/11/2026 | Kết quả đã ghi nhận |
| --- | --- |
| Backend với SQL Server | 119/119 kiểm thử đạt |
| Frontend | 40/40 kiểm thử đạt, build thành công |
| Edge với API thật và SQL Server | 10 kiểm tra đạt, gồm chỉnh sửa và lưu tài liệu, khôi phục phiên sau F5, đăng xuất, giải phóng máy sau khi đóng tab. |
| Trình duyệt Edge với API giả lập (mốc 24/11) | 3 kịch bản đạt: thêm phần mềm hàng loạt, xác nhận xóa và hồi quy luồng chọn máy → Desktop/Documents |

**Phần còn mở:** một file thuộc kho Agent cũ đang chờ thư mục nguồn để chuyển nội dung. Ticket nâng cao (phân công kỹ thuật viên, SLA, đính kèm) chưa nằm trong mốc nghiệm thu. Xem [tiến độ và bàn giao](TIEN_DO_DO_AN.md) để đối chiếu kết quả từng phiên và [kế hoạch tài khoản, phần mềm](docs/PLAN_PASSWORD_SOFTWARE.md) để tra cứu các quyết định nghiệp vụ.
