# Kế hoạch tiếp theo — chốt ngày 18/11/2026

> **Trạng thái 19/11/2026:** giữ main theo quyết định user, đã hoàn tất mật khẩu mặc định/reset, Employee tự đổi, nhắc theo chu kỳ và khóa quá hạn. Migration PasswordLifecycle đã backup/verify và áp dụng lên database hiện có, giữ nguyên6tài khoản, không ép đổi tài khoản cũ. Mã Agent/Nhật ký Admin của nhánh khác chưa merge.

## Yêu cầu đã chốt

### Chốt bổ sung 18/11 — triển khai trên main
- Tính theo thời gian thực: lần login đầu là mốc0, nhắc ngày5/6/7 tương ứng sau4/5/6ngày; khóa tại mốc đủ7ngày. Bỏ qua thông báo không dời hạn. Nhắc một lần mỗi mốc sau khi user bấm bỏ qua; đóng tab trước đó vẫn còn thông báo ở lần mở tiếp.
- Admin bật lại tài khoản khóa do hết hạn: giữ mật khẩu, bỏ mốc cũ, chu kỳ7ngày mới bắt đầu ở login tiếp theo.
- Thiết kế: bốn cột additive trên AppUser (yêu cầu đổi, login đầu, mốc nhắc đã xác nhận, khóa do hết hạn); tài khoản cũ mặc định không bị ép đổi. Policy tính bằng TimeProvider, dùng chung mọi máy. JWT validation chặn đúng hạn; worker quét định kỳ cập nhật trạng thái khi không có request.
- Default khi không cung cấp mật khẩu tạo user là12345Az@; UI tạo tài khoản dùng mặc định. API giữ tham số mật khẩu tùy chọn để tương thích các công cụ Admin hiện có. Employee mới luôn vào chu kỳ đổi mật khẩu dù Admin cung cấp mật khẩu riêng.
- Test clock biên trước/sau4/5/6/7ngày, chưa login không hết hạn, bỏ qua, reset, bật lại, đổi thành công; SQL test riêng. Backup/verify trước áp dụng migration thật.

- Mật khẩu mặc định cuối cùng: `12345Az@` (thay yêu cầu `1234Az@` ban đầu). Dùng cho user mới và khi Admin reset.
- Lần đăng nhập máy đầu tiên sau tạo/reset: hiện thông báo đổi mật khẩu, có thể đổi hoặc bỏ qua. Hạn theo tài khoản, không theo máy; đổi máy/F5/đăng nhập lại không bắt đầu lại hạn.
- Mốc 7 ngày bắt đầu từ lần đăng nhập đầu tiên sau tạo/reset. Nếu chưa đổi, nhắc lại ngày 5, 6, 7; hết hạn thì khóa tài khoản. Admin có thể bật lại. Đã chốt: bật lại giữ mật khẩu, chu kỳ mới7ngày từ lần login tiếp theo.
- Admin thêm app/service cho máy, từng mục hoặc hàng loạt. Có tên và loại. Các mục này không tính quota file và không ghi lịch sử file.
- Employee thấy danh sách/tên/loại nhưng không thấy đường dẫn. API Employee phải loại bỏ đường dẫn, không chỉ ẩn cột UI.
- Employee không tự xóa mục do Admin thêm. Khi yêu cầu xóa, popup xác thực tài khoản Admin ngay trong máy mô phỏng đó; không chuyển phiên Employee thành Admin.
- Ticket hỗ trợ tạm thời KHÔNG triển khai, chỉ giữ ghi chú. Không ưu tiên deploy.

## Thứ tự thực hiện

### 1. Reset mật khẩu — phần nhỏ độc lập

- API chỉ Admin, nút Reset ở bảng Tài khoản có xác nhận.
- Đặt mật khẩu `12345Az@`, tăng SecurityVersion để vô hiệu JWT và phiên máy cũ.
- Không reset Admin bootstrap, không tự bật tài khoản đã khóa, không reset hàng loạt tài khoản hiện có.
- Không ghi mật khẩu/token vào log. Không thay schema trong bước này.
- Đây CHỈ là thao tác reset: chưa có nhắc đổi mật khẩu hoặc bộ đếm 7 ngày.

### 2. Vòng đời mật khẩu — làm trọn luồng ở phiên tiếp theo

- Bổ sung theo yêu cầu 18/11: đã thêm hộp thoại **Thông tin tài khoản** từ máy Employee đang dùng, hiển thị thông tin tài khoản hiện tại và chức năng **Đổi mật khẩu**. Đã triển khai trên main; chưa có vòng đời nhắc/hạn7ngày.
- Employee phải có thể chủ động mở chức năng đổi mật khẩu, không chỉ thông qua popup nhắc lần đầu hoặc ngày 5/6/7. Dùng chung luồng đổi mật khẩu bên dưới, đồng bộ trạng thái nhắc/hạn khi đổi thành công. Phạm vi chỉnh sửa các thông tin khác của hồ sơ chưa được yêu cầu.
- Tạo user dùng mặc định từ backend, UI không yêu cầu Admin nghĩ mật khẩu. UI tạo user dùng mặc định; API giữ trường mật khẩu tùy chọn cho tương thích, không cung cấp thì dùng mặc định.
- Migration bổ sung trạng thái phải đổi mật khẩu, thời điểm đăng nhập đầu tiên/hạn chót và mốc nhắc đã hiển thị. Không tự ép tài khoản cũ nếu chưa được tạo/reset theo cơ chế mới.
- Endpoint đổi mật khẩu kiểm tra mật khẩu hiện tại, nhập lại mật khẩu mới, cập nhật Identity và thu hồi phiên cũ; không coi việc bỏ qua là đã đổi.
- Reset phải đánh dấu lại chu kỳ mới. Máy nào cũng dùng cùng trạng thái của tài khoản.
- UI thông báo lần đầu và ngày 5/6/7, cho bỏ qua trong hạn; lưu trạng thái trên server, không phụ thuộc localStorage.
- Khóa quá hạn phải được kiểm tra ở backend, bao gồm request dùng JWT/phiên máy đã mở, không chỉ chặn nút đăng nhập.
- Đã chốt: thời gian thực UTC; nhắc sau4/5/6ngày, khóa tại7ngày. Bỏ qua ghi nhận mốc trên server, nhắc lại ở mốc sau. Bật lại giữ mật khẩu và cấp chu kỳ mới từ login tiếp theo.
- Test đồng hồ giả: trước/đúng/sau hạn, đổi máy, F5, reset lần hai, đổi thành công, khóa và bật lại; backup trước migration.

### 3. Danh mục app/service do Admin quản lý

- Tận dụng danh sách Software hiện có: đã có tên/loại/đường dẫn và thêm từng dòng. Hiện Employee còn nhận đường dẫn; chưa đạt yêu cầu riêng tư mới.
- Tách dữ liệu danh mục khỏi LocalProfile/LocalFile để không ảnh hưởng quota hoặc lịch sử file của Employee.
- Dùng ID ổn định cho mục để xóa/cập nhật an toàn; phân loại App/Service, ghi chú do Admin thêm.
- Hàng loạt dùng một biểu mẫu cho cả hai cách. Bỏ qua mục trùng tên và phân loại trên từng máy.
- Popup Admin xác thực chỉ cấp quyền xóa mục cụ thể trên máy cụ thể, thời hạn ngắn và dùng một lần. Không lưu mật khẩu Admin, không thay JWT của Employee. Có giới hạn thử sai.
- Xác nhận phạm vi vẫn là mô phỏng web; không tự đưa Agent trở lại hoặc chạy cài đặt/service thật trên Windows.
- Test: Employee không lấy được path qua API, không xóa trực tiếp, xác thực sai/hết hạn/replay/sai máy đều bị chặn; quota và file history không thay đổi.

## Trạng thái

- Bước 1–2 đã hoàn tất trên main: default/reset, thông tin tài khoản/đổi mật khẩu, nhắc lần đầu/ngày 5–6–7, khóa 7 ngày và mở lại theo chu kỳ mới.
- Bước 3 mới hoàn tất **ẩn đường dẫn** ngày 23/11: API Employee loại bỏ `FilePath`, Admin vẫn xem được. Các phần ID, phân loại, xác nhận xóa và thêm hàng loạt đã được nghiệm thu riêng ngày 24/11 bên dưới. Không coi kiểm thử bảo mật đường dẫn là nghiệm thu toàn bộ bước 3.
- Ngày 19/11 sửa file PasswordReminder.tsx bị thiếu dù MachinesPage còn import; khôi phục smoke test. Sửa frontend/package.json chứa save-dev thành script chuyển tiếp vào admin, tạo lại lockfile đúng. Giữ node_modules cũ ngoài phạm vi xóa.
- Đã backup/verify và áp dụng 20261118024440_PasswordLifecycle; giữ 6 user, RequiresPasswordChange=false cho cả 6 user hiện có. Không reset mật khẩu tài khoản nghiệp vụ trong test.
- Backend kiểm tra hạn trên request JWT; worker quét mỗi phút khi không có request. Khi API tắt thì không có worker, nhưng request sau khi mở API sẽ kiểm tra hạn trước khi cấp quyền.

## Mốc tiếp tục 24/11 — App/Service có quản trị

Yêu cầu gốc đã được chốt: Admin khai báo phần mềm, Employee xem danh sách và chỉ xóa khi Admin xác nhận ngay trên máy đó. User xác nhận hàng loạt gồm **cả nhiều mục/một máy và cùng danh sách/nhiều máy**; mục trùng tên và phân loại được bỏ qua và giữ nguyên phiên bản, đường dẫn cũ.

Thiết kế triển khai: giữ `SoftwareJson` và bổ sung `id`, `kind` (App/Service, dữ liệu cũ chưa phân loại vẫn giữ nguyên `type`). Revision của cấu hình ngăn form cũ ghi đè dữ liệu mới. Xác thực Admin và xóa trong **cùng một request** gắn với đúng máy/mục/phiên Employee; không phát token Admin hoặc lưu quyền nâng cấp. Đây là lựa chọn kỹ thuật thay cho cấp grant riêng, vẫn chỉ cho phép đúng thao tác vừa xác nhận. Không thay schema, quota hoặc nhật ký file, không cài/xóa phần mềm Windows thật.

- [x] Backend: ID ổn định, revision và bảo vệ cập nhật cạnh tranh; endpoint xóa kiểm tra phiên máy + Admin active, giới hạn thử sai; test SQL riêng cho quyền, replay, xung đột và dữ liệu không liên quan.
- [x] Frontend: phân loại và ghi chú Admin quản lý; popup xác nhận xóa, không đổi phiên Employee, xóa mật khẩu khỏi form sau mỗi lần gửi/đóng; xử lý lỗi/thử lại và mobile.
- [x] Hàng loạt cho tối đa 50 máy mỗi lượt. Bỏ qua mục trùng trên từng máy, báo số thêm/bỏ qua và hủy cả lượt nếu vượt giới hạn 200 mục/máy.
- [x] Nghiệm thu (118/118 backend, 40/40 frontend, build đạt, 3 kịch bản Edge đạt): test backend, frontend, build, browser; cập nhật trạng thái, giới hạn và bước kế tiếp trong tiến độ. Không commit/push.
