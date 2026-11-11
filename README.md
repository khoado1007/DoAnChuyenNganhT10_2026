
# IT MANAGEMENT — ĐỒ ÁN ITAM/ITSM & WINDOWS DOMAIN SIMULATION

> **Tài liệu đặc tả ý tưởng và phạm vi đồ án chuyên ngành**  
> **Trạng thái:** Đặc tả mục tiêu cần triển khai, **không phải** báo cáo những tính năng đã hoàn thành.  
> **Nguồn cảm hứng:** FleetDM (quản lý thiết bị); mô phỏng một phần trải nghiệm đăng nhập và hồ sơ người dùng Windows. Không sao chép toàn bộ FleetDM hoặc Windows.

**Bàn giao tiến độ:** đọc [TIEN_DO_DO_AN.md](TIEN_DO_DO_AN.md) khi tiếp tục phiên mới; cập nhật sau mỗi phần việc lớn và trước khi dừng do giới hạn phiên.

## Cách chạy hiện tại — 11/11/2026

Dùng **SQL Server Express LocalDB** tại `(localdb)\MSSQLLocalDB`, Windows Authentication; **không dùng Docker**. Xem [RUNNING.md](RUNNING.md) để chạy API, web, Agent và bộ kiểm thử. LocalDB tự khởi động khi kết nối; không cần mở SSMS trước khi chạy API.

Các file Docker cũ chỉ được giữ làm lịch sử, không thuộc quy trình chạy hiện tại. Migration PostgreSQL cũ được giữ tham khảo và loại khỏi biên dịch; không áp dụng chúng cho SQL Server.

## 1. Tổng quan đề tài

Xây dựng ứng dụng quản lý tài sản công nghệ thông tin (**IT Asset Management — ITAM**) kết hợp một số nghiệp vụ hỗ trợ dịch vụ CNTT (**IT Service Management — ITSM**) dành cho mô hình doanh nghiệp nhỏ. Hệ thống giúp bộ phận IT quản lý nhân viên, thiết bị, cấp phát và thu hồi tài sản, quyền sử dụng máy, và vùng lưu trữ tài liệu cá nhân theo từng nhân viên trên từng thiết bị.

Điểm đặc trưng của đồ án là **Windows Domain Simulation trên trình duyệt**: người dùng chọn một máy công ty, đăng nhập bằng **Domain ID mô phỏng**, rồi làm việc trong **Desktop/Documents mô phỏng**. File người dùng tạo được lưu thật trên ổ đĩa, nhưng nằm trong thư mục riêng do ứng dụng/Agent quản lý. Không tạo tài khoản Windows thật, không thay đổi màn hình đăng nhập của hệ điều hành.

**Mục tiêu trình diễn chính:** Admin tạo nhân viên và thiết bị → cấp quyền sử dụng máy → nhân viên chọn máy và đăng nhập → hệ thống mở hoặc khởi tạo profile riêng → nhân viên tạo/quản lý tài liệu → quota số lượng file được kiểm soát → Admin có thể thu hồi quyền/trả thiết bị và xem lịch sử.

## 2. Công nghệ và cấu trúc hệ thống

| Thành phần | Công nghệ dự kiến | Trách nhiệm |
| --- | --- | --- |
| Frontend | React + TypeScript + Vite | Hai trải nghiệm: Admin Dashboard và Windows Simulation |
| Backend | ASP.NET Core Web API (.NET 10) | Xác thực, phân quyền, nghiệp vụ, API, điều phối Agent |
| Database | SQL Server LocalDB + Entity Framework Core | Danh tính, tài sản, quyền, metadata file, quota, lịch sử |
| Endpoint Agent | Python | Quản lý vùng file local theo `DeviceId` và `DomainUserId` |
| Triển khai | Windows trực tiếp | API, SQL Server LocalDB, React và Python Agent; không dùng Docker |

**Không sử dụng Rust API, MongoDB, OneDrive, Active Directory hay Windows Service cho MVP.** Không triển khai microservices nếu một Web API là đủ.

Có **hai instance Agent** cho mục đích trình diễn:

- **Real Agent:** chạy trên Windows thật, quản lý thư mục ứng dụng trên máy đó; có thể đọc một số thông tin cơ bản để nhận diện thiết bị khi được cho phép.
- **Simulated Agent:** mô phỏng một thiết bị khác bằng định danh, dữ liệu và vùng lưu trữ riêng. Hai Agent có thể chạy trên cùng một máy vật lý, **không được mô tả như hai máy vật lý độc lập**.

Server lưu **metadata** của file, còn **nội dung file** nằm trong thư mục cục bộ của Agent tương ứng. Trình duyệt không được truy cập trực tiếp ổ đĩa tùy ý; thao tác file phải đi qua lớp nghiệp vụ và quyền truy cập của hệ thống.

## 3. Vai trò và đối tượng sử dụng

| Vai trò | Quyền chính trong đồ án |
| --- | --- |
| **Admin** | Quản lý tài khoản, Domain ID mô phỏng, thiết bị, cấp phát/thu hồi, quyền đăng nhập, Agent, quota, metadata và lịch sử |
| **Employee** | Chọn máy đã được phép sử dụng, đăng nhập, truy cập profile riêng, tạo/sửa/xóa tài liệu và xem quota của mình |
| **Technician** | Vai trò ITSM giới hạn; xử lý ticket cơ bản khi phân hệ ticket được triển khai |

**Phân biệt ba khái niệm:**

- **Domain User:** danh tính nhân viên, **không phải** Agent.
- **Device:** tài sản/máy được doanh nghiệp quản lý.
- **Agent:** phần mềm liên kết với Device để thao tác với vùng lưu trữ cục bộ.

**Đăng ký Agent không đồng nghĩa tạo Domain ID.** Admin có thể thao tác trong một quy trình thiết lập, nhưng database và cơ chế xác thực phải tách biệt.

## 4. Hai giao diện cần xây dựng

### 4.1. Admin Dashboard

Giao diện quản trị nội bộ chuyên nghiệp, gọn và nhất quán, gồm:

1. **Dashboard:** số nhân viên, thiết bị, thiết bị đã cấp phát, phân bổ quota và các số liệu nghiệp vụ có nguồn API thật.
2. **Users/Domain Accounts:** tạo tài khoản, chỉnh sửa thông tin, khóa/mở, phân vai trò, quản lý Domain ID mô phỏng.
3. **Devices/Assets:** danh sách máy, mã tài sản, thông tin thiết bị, tạo mới và cập nhật trạng thái vòng đời.
4. **Assignments:** cấp phát, trả, thu hồi; ghi nhận người nhận và lịch sử.
5. **Device Access:** cấp/thu hồi quyền đăng nhập máy cho nhiều nhân viên, độc lập với người chịu trách nhiệm tài sản.
6. **Agents:** đăng ký Agent thật/mô phỏng và liên kết thiết bị; quản lý thông tin đăng ký. **Không cần dashboard online/offline hoặc heartbeat monitoring.**
7. **Storage Quota:** đặt số file tối đa cho từng cặp nhân viên–thiết bị, mặc định 10; xem số file đã sử dụng.
8. **File Metadata:** xem tên file, loại, kích thước, vị trí tương đối, chủ sở hữu và thiết bị; **không mặc định có quyền đọc nội dung file cá nhân**.
9. **Audit/History:** lịch sử cấp phát, thu hồi quyền, điều chỉnh quota và thao tác nghiệp vụ.
10. **Tickets (cơ bản):** nhân viên gửi yêu cầu hỗ trợ, Technician/Admin xem và cập nhật trạng thái; không xây dựng ITSM quy mô lớn.

Các route minh họa: `/admin`, `/admin/users`, `/admin/assets`, `/admin/assignments`, `/admin/agents`, `/admin/quotas`, `/admin/tickets`. Có thể điều chỉnh theo cấu trúc code hiện hữu.

### 4.2. Windows Simulation

Đây là một **trải nghiệm web đơn giản**, không tái tạo toàn bộ Windows 10/11. Có ba màn hình chính:

**Trang 1 — Chọn máy (`/machines`)**

- Hiển thị danh sách thiết bị mà hệ thống cho phép chọn; dữ liệu lấy từ API, không hardcode PC-001/PC-002.
- Chọn máy để mở màn hình đăng nhập. Khi máy bị thu hồi/vô hiệu hóa theo chính sách, không cho đăng nhập mới; cập nhật danh sách phù hợp.
- Admin có thể tạo thêm máy để demo (PC-003, PC-004...).

**Trang 2 — Đăng nhập Domain ID (`/machines/:deviceId/login`)**

- Nhập Domain ID mô phỏng (ví dụ `COMPANY\khoa`) và mật khẩu.
- Backend kiểm tra danh tính, trạng thái tài khoản và **quyền sử dụng đúng thiết bị**.
- Không đăng nhập Windows thật, không tạo SID hay profile Windows thật.

**Trang 3 — Desktop mô phỏng (`/machines/:deviceId/desktop`)**

- Hiển thị Desktop, Documents, File Manager, tên người dùng, tên máy, bộ đếm quota và Đăng xuất.
- Tạo, xem, đổi tên, di chuyển, xóa file tài liệu trong vùng profile riêng.
- Có trạng thái trống, lỗi, đang xử lý và thông báo quota; UI đơn giản, dễ trình diễn.
- Không bắt buộc Start Menu đầy đủ, window manager phức tạp, hiệu ứng động hoặc desktop remote thực tế.

**Một React SPA có thể sử dụng hai layout độc lập**: `AdminLayout` và `SimulationLayout`. Frontend chỉ giới hạn đường dẫn để cải thiện UX; phân quyền thực sự do API thực thi.

## 5. Quản lý thiết bị và cấp phát

- Admin tạo, sửa và tra cứu tài sản/máy công ty.
- Mỗi máy có `DeviceId` ổn định, mã tài sản, thông tin và trạng thái vòng đời.
- Ghi nhận quy trình **cấp phát → trả/thu hồi** bằng lịch sử không bị ghi đè tùy tiện.
- **Người chịu trách nhiệm tài sản** khác với **danh sách người được phép sử dụng**.
- **Một máy có thể được nhiều nhân viên đăng nhập**, nhưng một phiên Desktop mô phỏng chỉ đại diện cho một nhân viên tại một thời điểm.
- Thu hồi quyền đăng nhập phải chặn thao tác được bảo vệ tiếp theo; **không tự xóa file hoặc profile của người đã bị thu hồi**.

## 6. Domain ID và Local User Profile mô phỏng

Một profile được định danh bằng **cặp `(DomainUserId, DeviceId)`**.

Ví dụ:

- `COMPANY\khoa` trên `PC-001` có Desktop/Documents và quota của riêng mình.
- `COMPANY\an` trên `PC-001` có Desktop/Documents độc lập, không thấy file của Khoa.
- `COMPANY\khoa` đăng nhập `PC-002` lần đầu được **tạo profile mới, trống**; không mang file từ `PC-001`.
- Khoa đăng xuất rồi đăng nhập lại `PC-001` thì **file cũ vẫn còn**. Chỉ lần đầu ở máy mới mới khởi tạo profile, không reset mỗi lần đăng nhập.

Cấu trúc file vật lý minh họa:

```text
ITManagementSim/
└── Devices/
    ├── <DeviceId-PC001>/
    │   ├── System/                # Cấu hình/log ứng dụng, ngoài quota
    │   └── Profiles/
    │       ├── <UserId-Khoa>/
    │       │   ├── Desktop/
    │       │   └── Documents/
    │       └── <UserId-An>/
    │           ├── Desktop/
    │           └── Documents/
    └── <DeviceId-PC002>/
        ├── System/
        └── Profiles/
            └── <UserId-Khoa>/
                ├── Desktop/
                └── Documents/
```

Đường dẫn thực được cấu hình riêng theo Agent; không sử dụng trực tiếp Domain ID hoặc tên file chưa kiểm tra để ghép đường dẫn hệ thống. Các thư mục `Desktop`/`Documents` ở đây **chỉ mô phỏng**, không phải `C:\Users\...` của Windows.

## 7. Quản lý file tài liệu cá nhân

### 7.1. Loại file và thao tác

Phạm vi ưu tiên các file tài liệu: `.doc`, `.docx`, `.xls`, `.xlsx`, `.pdf`, `.ppt`, `.pptx`, `.txt`, `.csv`; có thể mở rộng danh sách định dạng được phép khi cần. Không phục vụ file thực thi, file hệ thống hay duyệt ổ đĩa tùy ý.

Trong File Manager mô phỏng, nhân viên có thể:

- Tạo file tài liệu mới trong Desktop/Documents.
- Xem danh sách và thông tin file.
- Mở/xem nội dung với định dạng hỗ trợ; **không hứa chỉnh sửa trực tiếp mọi định dạng Office** khi chưa tích hợp editor phù hợp.
- Sửa file trong phạm vi tính năng được triển khai, đổi tên, di chuyển và xóa file.
- Xem quota đã dùng; thao tác tạo/xóa được phản ánh lại trên UI.

**Lưu file tự động theo thao tác:** khi nhân viên tạo/sửa file trong giao diện mô phỏng, hệ thống ghi file vào thư mục cục bộ thông qua Agent sau khi kiểm tra quyền. **Không yêu cầu nút “Upload lên cloud” và không đồng bộ file giữa các máy.**

### 7.2. Metadata và quyền riêng tư

SQL Server LocalDB lưu `FileId`, `LocalProfileId`, tên file, phần mở rộng, kích thước, đường dẫn tương đối, thời điểm tạo/sửa và trạng thái cần thiết. Nội dung file nằm tại máy tương ứng. Admin có thể quản trị quota và xem metadata, **không có quyền đọc nội dung mặc định**.

File không được phục vụ công khai qua static web. Backend/Agent phải xác minh quyền trên từng thao tác, chống path traversal, symbolic link escape, ghi đè ngoài vùng root và tên file nguy hiểm.

## 8. Quota theo **số lượng file**

**Chính sách đã chốt:**

- Mặc định **10 file cho mỗi `(DomainUserId, DeviceId)`**; Admin có thể điều chỉnh cho từng profile.
- `PC-001/Khoa`: 10 file; `PC-002/Khoa`: 10 file độc lập; `PC-001/An`: 10 file độc lập.
- Tất cả **file do người dùng tạo** trong Desktop/Documents mô phỏng đều tính quota, **bất kể tên, phần mở rộng hay thuộc tính Hidden**.
- Chỉ **file cấu hình nội bộ thực sự do ứng dụng tạo và nằm ngoài Desktop/Documents** được miễn quota.
- Thư mục không tính là file, nhưng file bên trong vẫn tính.

| Thao tác | Thay đổi số file đang dùng |
| --- | --- |
| Tạo file mới / sao chép ra file mới | +1 |
| Sửa, đổi tên, di chuyển trong cùng profile | 0 |
| Tạo thư mục | 0 |
| Xóa file thành công | −1 |
| Khôi phục file đã xóa (nếu có chức năng) | +1, phải còn quota |
| File cấu hình nội bộ do ứng dụng tạo ngoài Desktop/Documents | 0 |

- Khi đạt 10/10, từ chối tạo thêm file qua ứng dụng, có thông báo rõ ràng; **không tự động xóa file**.
- Kiểm tra quota phải thực hiện tại phía tin cậy (backend/Agent), không chỉ ở frontend; bảo vệ các thao tác đồng thời để tránh vượt mức.
- Quota ứng dụng **không phải NTFS Disk Quota**: chương trình Windows khác ghi trực tiếp vào thư mục có thể không bị ngăn bởi giới hạn này.
- Không dùng GB làm đơn vị quota của MVP. Kích thước byte của file vẫn có thể lưu trong metadata để hiển thị/giới hạn an toàn kỹ thuật độc lập nếu cần.

## 9. Agent và cơ chế thao tác file

- **Đăng ký Agent gắn với Device**, không tạo Domain ID nhân viên thay cho quy trình tài khoản.
- Python Agent quản lý vùng file local, tạo/mở profile, thao tác file theo quyền và trả kết quả về hệ thống.
- Real Agent và Simulated Agent có **vùng dữ liệu, định danh và credential tách biệt**.
- Không cần màn hình online/offline, heartbeat status hay thu thập giám sát liên tục trong MVP.
- Khi một thao tác cần Agent nhưng Agent không khả dụng, ứng dụng phải báo lỗi đúng, **không giả vờ đã lưu thành công**.
- Không đọc Desktop/Documents thật của Windows, không quét toàn bộ ổ đĩa, không sửa tài khoản Windows hay chạy lệnh hệ thống từ xa.

Cơ chế truyền yêu cầu đến Agent phải được xác thực và kiểm thử thực tế. Trong môi trường demo, có thể thiết kế một giao thức request/response hoặc hàng đợi tác vụ ngắn, nhưng **không biến nó thành chức năng đồng bộ file giữa thiết bị**.

## 10. Các nghiệp vụ ITSM phụ trợ

Đồ án có thể triển khai **ticket cơ bản** phục vụ mô phỏng hỗ trợ CNTT:

- Employee tạo ticket với tiêu đề, mô tả, thiết bị liên quan (nếu có).
- Admin/Technician xem danh sách, phân loại hoặc cập nhật trạng thái.
- Employee xem tiến độ ticket của mình.

Không xây dựng SLA phức tạp, workflow nhiều cấp, hệ thống helpdesk đầy đủ hoặc tự động điều khiển máy qua ticket. **Ưu tiên hoàn thành luồng ITAM + Windows Simulation + file quota trước phân hệ ticket.**

## 11. Mô hình dữ liệu khái niệm

| Nhóm bảng/thực thể | Mục đích |
| --- | --- |
| `Users` / `DomainUsers` | Danh tính và Domain ID mô phỏng; mật khẩu phải băm an toàn |
| `Roles` | Admin, Employee, Technician |
| `Devices` / `Assets` | Thiết bị và vòng đời tài sản |
| `DeviceAssignments` | Lịch sử cấp phát/trả/thu hồi |
| `DeviceAccessGrants` | Quyền đăng nhập thiết bị theo nhân viên |
| `AgentRegistrations` | Agent và mối liên kết Device |
| `LocalProfiles` | Cặp Domain User + Device, duy nhất |
| `StorageQuotas` | `maxFiles`, `usedFiles` theo Local Profile |
| `FileMetadata` | File vật lý thuộc profile, không chứa nội dung file |
| `AuditLogs` | Nhật ký thay đổi/quyền/nghiệp vụ |
| `Tickets` (phụ trợ) | Yêu cầu hỗ trợ cơ bản |

Các tên bảng là thiết kế mục tiêu, cần đối chiếu với schema/EF Core migrations hiện có trước khi sửa. Không tạo bảng trùng hoặc xóa dữ liệu cũ khi chưa có kế hoạch migration/backup.

## 12. Bảo mật và giới hạn mô phỏng

1. Mật khẩu được băm an toàn; không hardcode tài khoản mặc định cho môi trường dùng chung.
2. Domain ID mô phỏng được xác thực bởi backend, **không tương đương Windows/AD authentication**.
3. Quyền đăng nhập thiết bị và quyền truy cập Local Profile phải được kiểm tra server-side ở mọi thao tác có bảo vệ.
4. Agent credential khác user credential; Agent không tự trở thành chủ sở hữu file.
5. Không bao giờ tin đường dẫn tuyệt đối, `userId`, `deviceId` hay role do client gửi là bằng chứng có quyền.
6. Chặn truy cập chéo profile, path traversal, symlink và upload/ghi file vượt phạm vi cho phép.
7. Không cho Admin đọc nội dung file cá nhân chỉ vì là Admin; chỉ xem metadata theo phân quyền.
8. Thu hồi quyền không tự xóa hồ sơ/file. Không xóa toàn bộ dữ liệu khi đăng xuất.
9. Các thao tác ghi file và metadata cần có cơ chế phục hồi/thông báo khi một phía thất bại.
10. Mô phỏng nhiều user trong cùng **Windows account thật không tạo ra cách ly bảo mật ở cấp hệ điều hành**; chỉ có thể bảo vệ trong ứng dụng.

## 13. Kịch bản trình diễn và tiêu chí nghiệm thu

1. Admin tạo ít nhất hai tài khoản `COMPANY\khoa`, `COMPANY\an`.
2. Admin tạo `PC-001`, `PC-002`, liên kết Real Agent và Simulated Agent (nhãn rõ ràng).
3. Admin cấp phát một máy cho người chịu trách nhiệm, đồng thời cho phép nhiều nhân viên đăng nhập máy đó.
4. Trang chọn máy lấy dữ liệu thật từ API; Admin thêm máy mới thì giao diện cập nhật.
5. Nhân viên chọn máy, đăng nhập đúng Domain ID; đăng nhập sai hoặc không có quyền thì bị từ chối.
6. Khoa đăng nhập PC-001 lần đầu: Desktop/Documents trống, quota `0/10`.
7. Khoa tạo 10 file tài liệu: quota `10/10`; tạo file thứ 11 bị từ chối, dữ liệu 10 file đầu vẫn nguyên vẹn.
8. Đổi tên/sửa/di chuyển file không làm tăng quota; xóa file giảm quota.
9. File cấu hình hệ thống ngoài Desktop/Documents không tính quota; file người dùng tạo tên `desktop.ini` trong Desktop vẫn tính.
10. An đăng nhập PC-001 thấy profile riêng, không thấy file của Khoa; quota của An độc lập.
11. Khoa đăng nhập PC-002 lần đầu thấy profile trống, `0/10`; đăng nhập lại PC-001 vẫn thấy file cũ.
12. Admin điều chỉnh quota một profile; nhân viên nhìn thấy quota mới và hệ thống thực thi đúng.
13. Admin thu hồi quyền đăng nhập; nhân viên không thể tiếp tục thực hiện thao tác được bảo vệ; file không bị xóa tự động.
14. Admin xem được file metadata và lịch sử theo quyền, nhưng không mặc định tải được nội dung file cá nhân.
15. Khi Agent hoặc storage lỗi, ứng dụng thông báo lỗi, không ghi nhận thao tác như đã thành công.
16. Nếu triển khai ticket cơ bản: Employee tạo ticket, Technician/Admin cập nhật, Employee thấy trạng thái.

## 14. Thứ tự ưu tiên triển khai

**Giai đoạn 1 — Nền tảng:** cấu trúc React/API/SQL Server LocalDB, xác thực, RBAC, tài khoản, thiết bị, cấp phát và quyền truy cập.

**Giai đoạn 2 — Profile & Agent:** đăng ký Agent, tạo profile `(user, device)` độc lập, thao tác file local, metadata và quota 10 file.

**Giai đoạn 3 — Hai UI:** Admin Dashboard và Windows Simulation đủ ba màn hình; liên kết API thật, xử lý lỗi và dữ liệu trống.

**Giai đoạn 4 — Kiểm thử:** luồng nghiệm thu, test phân quyền, kiểm tra quota đồng thời, cách ly profile, dữ liệu tồn tại sau đăng xuất, xử lý lỗi Agent.

**Giai đoạn 5 — Hoàn thiện:** ticket cơ bản nếu còn trong kế hoạch, hướng dẫn demo, sơ đồ thiết kế, cập nhật README và báo cáo kết quả.

Dự án nên hoàn thành một luồng nghiệp vụ chạy được từ đầu đến cuối trước khi mở rộng chức năng phụ.

## 15. Ngoài phạm vi đồ án — chỉ ghi nhận định hướng tương lai

- Tích hợp Windows Domain/Active Directory thật và đăng nhập trực tiếp vào hệ điều hành.
- Tạo Windows Service tự khởi động theo phiên bật máy, triển khai quản lý Agent ở quy mô lớn.
- Điều khiển từ xa như thay đổi hình nền/chính sách Windows, xem log Windows có phân quyền.
- NTFS hard quota thật, Windows user/SID/profile thật.
- Roaming profile, đồng bộ hoặc tải file giữa các máy như OneDrive.
- Giám sát thiết bị online/offline liên tục, remote shell, remote desktop.

**Các ý tưởng trên không được Agent tự triển khai trong MVP chỉ vì chúng xuất hiện trong tài liệu.**

## 16. Quy tắc dành cho AI Coding Agent

- Trước khi code, đọc README và cấu trúc repository; **không hỏi lại các quyết định đã chốt**.
- Với yêu cầu mới còn nhiều lựa chọn độc lập, hỏi trắc nghiệm ngắn, ưu tiên các câu thực sự ảnh hưởng đến kiến trúc/khả năng nghiệm thu.
- Giữ một ASP.NET Core Web API chính; không tự thêm Rust, MongoDB hoặc microservice.
- Code sạch, tách trách nhiệm, đặt tên rõ ràng, tránh abstraction không cần thiết.
- Comment **tiếng Việt kèm thuật ngữ tiếng Anh chuyên ngành** ở logic nghiệp vụ hoặc điểm khó, không comment mọi dòng.
- Frontend chuyên nghiệp, màu phẳng, không gradient; Admin có thể dùng Georgia ở tiêu đề; Windows Simulation ưu tiên font UI dễ đọc và thao tác đơn giản.
- Bảo mật backend là bắt buộc; không dựa vào route guard phía React làm biện pháp duy nhất.
- Sau task: chạy build/lint/tests phù hợp, ghi rõ phần chưa kiểm chứng; dọn **chỉ file tạm an toàn** do task tạo, không tự xóa profile, dữ liệu, migration, volume hoặc backup.

---

**Tuyên bố phạm vi:** Tài liệu này mô tả **toàn bộ chức năng dự định xây dựng trong phạm vi đồ án** và các giới hạn của MVP. Nó **không khẳng định** mọi chức năng đã được hiện thực trong repository. Hiện trạng code, lệnh chạy và các endpoint đang tồn tại cần được đối chiếu riêng với source trước khi viết báo cáo hoàn thành.
# DoAnChuyenNganhT10_2026
