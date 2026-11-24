# Thông tin tài khoản Employee — 18/11/2026

## Bản nền
User xác nhận tiếp tục trên `main` tại 0e221a5. Nhánh này còn runtime Agent và chưa có Admin reset/nhật ký của nhánh feature/admin-audit. Không tự merge hay phục hồi các thay đổi đó.

## Phạm vi thực hiện
1. Nút Thông tin tài khoản trong máy đang đăng nhập mở hộp thoại thông tin tài khoản lấy từ `/auth/me`, chỉ đọc họ tên/tên đăng nhập/email/phòng ban/trạng thái.
2. Employee nhập mật khẩu hiện tại, mật khẩu mới và xác nhận. POST `/auth/change-password` chỉ dùng danh tính JWT, không nhận userId làm quyền thao tác. Mật khẩu mới khác hiện tại, tối thiểu8 ký tự và thỏa Identity policy.
3. Transaction khóa tài khoản, xác thực mật khẩu hiện tại, thay Identity hash và tăng SecurityVersion. Token và phiên máy cũ bị thu hồi; giao diện xóa tham chiếu phiên/nội dung cá nhân và đưa về đăng nhập máy với thông báo thành công.
4. Chỉ mở hộp thoại khi không có thao tác file đang chạy. Đóng hộp thoại khi chưa đổi giữ phiên máy và nội dung đang soạn. Chế độ demo hiển thị thông tin nhưng không giả lập đổi mật khẩu thành công.

## Kiểm chứng
- SQL integration: anonymous/Admin bị chặn, sai mật khẩu/yếu/không khớp/trùng cũ không đổi dữ liệu, thành công thu hồi token mọi tab và phiên máy; đăng nhập lại được, tài khoản khác không bị ảnh hưởng.
- Browser mock: tải/lỗi/thử lại thông tin, xác nhận mật khẩu, API lỗi, thành công xóa phiên và về đăng nhập; desktop/mobile.
- BuildAPI/UI và tests hiện hữu liên quan; không migration, không reset tài khoản nghiệp vụ.

## Làm sau
Mật khẩu mặc định và reset của Admin trên main; chu kỳ lần đầu/ngày5–6–7/khóa sau7ngày; app/service. Không tự khẳng định đã có nhắc hạn khi mới hoàn thành đổi mật khẩu chủ động. Ticket tạm hoãn.
