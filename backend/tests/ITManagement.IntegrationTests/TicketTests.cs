using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ITManagement.Domain.Entities.Ticket;
using ITManagement.Domain.Enums;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ITManagement.IntegrationTests;

/// <summary>
/// Kiểm thử ticket cơ bản: Employee tạo và chỉ thấy ticket của mình; Admin xử lý theo luồng
/// Open → InProgress → Resolved → Closed; bình luận công khai/nội bộ và ranh giới vai trò.
/// </summary>
public sealed class TicketTests(ServerApiFactory factory) : IClassFixture<ServerApiFactory>
{
    private static async Task<JsonElement> Data(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {text}");
        return JsonDocument.Parse(text).RootElement.GetProperty("data").Clone();
    }

    private async Task<HttpClient> Login(string username, string password)
    {
        var client = factory.CreateClient();
        var auth = await Data(await client.PostAsJsonAsync("/api/auth/login", new { username, password }));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.GetProperty("accessToken").GetString());
        return client;
    }

    private async Task<HttpClient> Admin() => await Login("admin", "Admin@12345");

    private async Task<(HttpClient Client, Guid Id, string Username)> CreateUser(HttpClient admin, UserRole role, string label)
    {
        var name = label + Guid.NewGuid().ToString("N")[..8];
        var response = await admin.PostAsJsonAsync("/api/users", new
        {
            username = name,
            email = name + "@test.local",
            fullName = "Ticket " + label,
            department = "IT",
            password = "Test@12345",
            role = role.ToString()
        });
        var data = await Data(response);
        return (await Login(name, "Test@12345"), data.GetProperty("id").GetGuid(), name);
    }

    private static Task<HttpResponseMessage> CreateTicket(HttpClient client, string title, string token,
        string category = "Hardware", string priority = "Medium") =>
        client.PostAsJsonAsync("/api/tickets", new
        {
            title = $"{token} - {title}",
            description = "Mô tả sự cố cần bộ phận CNTT hỗ trợ kiểm tra.",
            category,
            priority
        });

    private async Task<JsonElement> CreateTicketOrFail(HttpClient client, string title, string token,
        string category = "Hardware", string priority = "Medium")
        => await Data(await CreateTicket(client, title, token, category, priority));

    [Fact]
    public async Task Employee_creates_ticket_and_only_sees_own_ticket()
    {
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/tickets")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await CreateTicket(anonymous, "Không đăng nhập", "NOAUTH")).StatusCode);

        using var admin = await Admin();
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateTicket(admin, "Admin tạo hộ", "ADMIN")).StatusCode);

        var (employeeA, userIdA, _) = await CreateUser(admin, UserRole.Employee, "empa");
        var (employeeB, _, _) = await CreateUser(admin, UserRole.Employee, "empb");
        var (technician, _, _) = await CreateUser(admin, UserRole.Technician, "tech");
        using var a = employeeA; using var b = employeeB; using var t = technician;

        var created = await CreateTicketOrFail(a, "Máy tính không khởi động", "TKTEST");
        var id = created.GetProperty("id").GetGuid();
        Assert.Matches(@"^TKT-\d{4}-\d{4}$", created.GetProperty("ticketNumber").GetString());
        Assert.Equal("Open", created.GetProperty("status").GetString());
        Assert.Equal(userIdA, created.GetProperty("createdByUserId").GetGuid());
        Assert.Equal("Ticket empa", created.GetProperty("createdByName").GetString());
        Assert.EndsWith("Z", created.GetProperty("createdAt").GetString());

        var own = await Data(await a.GetAsync("/api/tickets?pageSize=50"));
        Assert.Contains(own.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.EndsWith("Z", own.GetProperty("items")[0].GetProperty("createdAt").GetString());

        var otherEmployee = await Data(await b.GetAsync("/api/tickets?pageSize=50"));
        Assert.DoesNotContain(otherEmployee.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        var technicianList = await Data(await t.GetAsync("/api/tickets?pageSize=50"));
        Assert.DoesNotContain(technicianList.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);

        Assert.Equal(HttpStatusCode.NotFound, (await b.GetAsync($"/api/tickets/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await t.GetAsync($"/api/tickets/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await a.GetAsync($"/api/tickets/{id}")).StatusCode);
        var adminList = await Data(await admin.GetAsync("/api/tickets?pageSize=100"));
        Assert.Contains(adminList.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/tickets/{id}")).StatusCode);

        // Số ticket tăng và không trùng.
        var second = await CreateTicketOrFail(a, "Sự cố thứ hai", "TKTEST");
        Assert.NotEqual(created.GetProperty("ticketNumber").GetString(), second.GetProperty("ticketNumber").GetString());
    }

    [Fact]
    public async Task Admin_status_flow_enforces_transitions_and_resolution()
    {
        using var admin = await Admin();
        var (employee, _, _) = await CreateUser(admin, UserRole.Employee, "flow");
        using var a = employee;
        var created = await CreateTicketOrFail(a, "Cài phần mềm", "FLOW");
        var id = created.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await a.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "InProgress" })).StatusCode);

        // Không được nhảy trạng thái hoặc tự đóng khi chưa xử lý.
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "Resolved", resolution = "Xong" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "Closed" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "Assigned" })).StatusCode);

        var started = await Data(await admin.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "InProgress", note = "Đã tiếp nhận" }));
        Assert.Equal("InProgress", started.GetProperty("status").GetString());
        Assert.Equal(1, started.GetProperty("history").GetArrayLength());
        Assert.Equal("Đã tiếp nhận", started.GetProperty("history")[0].GetProperty("note").GetString());

        var noResolution = await admin.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "Resolved" });
        Assert.Equal(HttpStatusCode.BadRequest, noResolution.StatusCode);
        var blankResolution = await admin.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "Resolved", resolution = "   " });
        Assert.Equal(HttpStatusCode.BadRequest, blankResolution.StatusCode);

        var resolved = await Data(await admin.PatchAsJsonAsync($"/api/tickets/{id}/status",
            new { status = "Resolved", resolution = "Đã cài đặt và kiểm tra xong." }));
        Assert.Equal("Resolved", resolved.GetProperty("status").GetString());
        Assert.Equal("Đã cài đặt và kiểm tra xong.", resolved.GetProperty("resolution").GetString());
        Assert.EndsWith("Z", resolved.GetProperty("resolvedAt").GetString());
        Assert.Equal(2, resolved.GetProperty("history").GetArrayLength());

        var closed = await Data(await admin.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "Closed" }));
        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.Equal(3, closed.GetProperty("history").GetArrayLength());
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/tickets/{id}/status", new { status = "InProgress" })).StatusCode);

        var list = await Data(await admin.GetAsync($"/api/tickets?search=FLOW&pageSize=20"));
        var row = list.GetProperty("items").EnumerateArray().Single(x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal("Closed", row.GetProperty("status").GetString());
        Assert.EndsWith("Z", row.GetProperty("updatedAt").GetString());
    }

    [Fact]
    public async Task Comments_respect_visibility_and_internal_notes()
    {
        using var admin = await Admin();
        var (employee, _, username) = await CreateUser(admin, UserRole.Employee, "cmt");
        var (other, _, _) = await CreateUser(admin, UserRole.Employee, "cmtb");
        using var a = employee; using var b = other;
        var created = await CreateTicketOrFail(a, "Lỗi đăng nhập", "CMT");
        var id = created.GetProperty("id").GetGuid();

        var publicByEmployee = await Data(await a.PostAsJsonAsync($"/api/tickets/{id}/comments", new { content = "Tôi thử lại vẫn lỗi." }));
        Assert.False(publicByEmployee.GetProperty("isInternal").GetBoolean());
        Assert.Equal(username, publicByEmployee.GetProperty("authorUsername").GetString());
        Assert.EndsWith("Z", publicByEmployee.GetProperty("createdAt").GetString());

        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/tickets/{id}/comments",
            new { content = "Ghi chú nội bộ", isInternal = true })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await a.PostAsJsonAsync($"/api/tickets/{id}/comments",
            new { content = "   " })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync($"/api/tickets/{id}/comments",
            new { content = "Tôi không được phép" })).StatusCode);

        await Data(await admin.PostAsJsonAsync($"/api/tickets/{id}/comments", new { content = "Đã nhận yêu cầu." }));
        var internalNote = await Data(await admin.PostAsJsonAsync($"/api/tickets/{id}/comments",
            new { content = "Kiểm tra log máy trạm.", isInternal = true }));
        Assert.True(internalNote.GetProperty("isInternal").GetBoolean());

        var asEmployee = await Data(await a.GetAsync($"/api/tickets/{id}"));
        var employeeComments = asEmployee.GetProperty("comments").EnumerateArray().ToList();
        Assert.Equal(2, employeeComments.Count);
        Assert.DoesNotContain(employeeComments, c => c.GetProperty("content").GetString()!.Contains("log máy trạm"));
        Assert.DoesNotContain(employeeComments, c => c.GetProperty("isInternal").GetBoolean());

        var asAdmin = await Data(await admin.GetAsync($"/api/tickets/{id}"));
        var adminComments = asAdmin.GetProperty("comments").EnumerateArray().ToList();
        Assert.Equal(3, adminComments.Count);
        Assert.Contains(adminComments, c => c.GetProperty("isInternal").GetBoolean()
            && c.GetProperty("content").GetString() == "Kiểm tra log máy trạm.");
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsJsonAsync($"/api/tickets/{Guid.NewGuid()}/comments",
            new { content = "Không tồn tại" })).StatusCode);
    }

    [Fact]
    public async Task List_filters_paginates_and_clamps()
    {
        using var admin = await Admin();
        var (employee, _, _) = await CreateUser(admin, UserRole.Employee, "list");
        var (other, _, _) = await CreateUser(admin, UserRole.Employee, "listb");
        using var a = employee; using var b = other;
        var token = "LIST" + Guid.NewGuid().ToString("N")[..6];

        await CreateTicketOrFail(a, "Phiếu một", token, "Hardware", "High");
        await CreateTicketOrFail(a, "Phiếu hai", token, "Software", "Low");
        await CreateTicketOrFail(a, "Phiếu ba", token, "Network", "Critical");
        await CreateTicketOrFail(b, "Phiếu người khác", token, "Hardware", "High");

        var all = await Data(await admin.GetAsync($"/api/tickets?search={token}&pageSize=2"));
        Assert.Equal(4, all.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, all.GetProperty("items").GetArrayLength());
        Assert.True(all.GetProperty("hasNextPage").GetBoolean());

        var page2 = await Data(await admin.GetAsync($"/api/tickets?search={token}&page=2&pageSize=2"));
        Assert.Equal(2, page2.GetProperty("items").GetArrayLength());
        Assert.Equal(2, page2.GetProperty("page").GetInt32());

        var hardware = await Data(await admin.GetAsync($"/api/tickets?search={token}&category=Hardware"));
        Assert.Equal(2, hardware.GetProperty("totalCount").GetInt32());
        var high = await Data(await admin.GetAsync($"/api/tickets?search={token}&priority=High"));
        Assert.Equal(2, high.GetProperty("totalCount").GetInt32());
        var open = await Data(await admin.GetAsync($"/api/tickets?search={token}&status=Open"));
        Assert.Equal(4, open.GetProperty("totalCount").GetInt32());
        var closed = await Data(await admin.GetAsync($"/api/tickets?search={token}&status=Closed"));
        Assert.Equal(0, closed.GetProperty("totalCount").GetInt32());

        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/tickets?search={token}&page=0&pageSize=0")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/tickets?search={token}&pageSize=101")).StatusCode);

        var own = await Data(await a.GetAsync($"/api/tickets?search={token}&pageSize=20"));
        Assert.Equal(3, own.GetProperty("totalCount").GetInt32());
        Assert.All(own.GetProperty("items").EnumerateArray(),
            x => Assert.Equal("Ticket list", x.GetProperty("createdByName").GetString()));
    }

    [Fact]
    public async Task Admin_assigns_ticket_and_assigned_technician_processes_it()
    {
        using var admin = await Admin();
        var (employee, _, _) = await CreateUser(admin, UserRole.Employee, "asg");
        var (technician, technicianId, _) = await CreateUser(admin, UserRole.Technician, "asgt");
        var (otherTechnician, _, _) = await CreateUser(admin, UserRole.Technician, "asgo");
        using var a = employee; using var t = technician; using var o = otherTechnician;

        var created = await CreateTicketOrFail(a, "Cần kỹ thuật viên", "ASSIGN");
        var id = created.GetProperty("id").GetGuid();

        // 1. Employee và Technician khác không được phân công
        Assert.Equal(HttpStatusCode.Forbidden, (await a.PatchAsJsonAsync($"/api/tickets/{id}/assign",
            new { assignedToUserId = technicianId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await t.PatchAsJsonAsync($"/api/tickets/{id}/assign",
            new { assignedToUserId = technicianId })).StatusCode);

        // 2. Phân công cho user không hợp lệ (không tồn tại hoặc không phải Technician) trả 400
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PatchAsJsonAsync($"/api/tickets/{id}/assign",
            new { assignedToUserId = Guid.NewGuid() })).StatusCode);

        // 3. Admin phân công cho Kỹ thuật viên
        var assignedDetail = await Data(await admin.PatchAsJsonAsync($"/api/tickets/{id}/assign",
            new { assignedToUserId = technicianId, note = "Giao xử lý gấp" }));
        Assert.Equal(technicianId, assignedDetail.GetProperty("assignedToUserId").GetGuid());
        Assert.Equal("Ticket asgt", assignedDetail.GetProperty("assignedToName").GetString());

        // 4. Kỹ thuật viên được giao thấy ticket; kỹ thuật viên khác không thấy
        var assignedList = await Data(await t.GetAsync("/api/tickets?pageSize=50"));
        Assert.Contains(assignedList.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.OK, (await t.GetAsync($"/api/tickets/{id}")).StatusCode);

        var notAssignedList = await Data(await o.GetAsync("/api/tickets?pageSize=50"));
        Assert.DoesNotContain(notAssignedList.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == id);
        Assert.Equal(HttpStatusCode.NotFound, (await o.GetAsync($"/api/tickets/{id}")).StatusCode);

        // 5. Kỹ thuật viên chưa được giao không được cập nhật trạng thái
        Assert.Equal(HttpStatusCode.Forbidden, (await o.PatchAsJsonAsync($"/api/tickets/{id}/status",
            new { status = "InProgress" })).StatusCode);

        // 6. Kỹ thuật viên được giao cập nhật trạng thái: Open -> InProgress -> Resolved
        var inProgress = await Data(await t.PatchAsJsonAsync($"/api/tickets/{id}/status",
            new { status = "InProgress", note = "Kỹ thuật viên bắt đầu xử lý" }));
        Assert.Equal("InProgress", inProgress.GetProperty("status").GetString());

        var resolved = await Data(await t.PatchAsJsonAsync($"/api/tickets/{id}/status",
            new { status = "Resolved", resolution = "Đã thay cáp mạng và kiểm tra thông mạng thành công." }));
        Assert.Equal("Resolved", resolved.GetProperty("status").GetString());
        Assert.Equal("Đã thay cáp mạng và kiểm tra thông mạng thành công.", resolved.GetProperty("resolution").GetString());

        // 7. Admin hủy phân công ticket
        var unassigned = await Data(await admin.PatchAsJsonAsync($"/api/tickets/{id}/assign",
            new { assignedToUserId = (Guid?)null, note = "Thu hồi phân công" }));
        Assert.Equal(JsonValueKind.Null, unassigned.GetProperty("assignedToUserId").ValueKind);
        Assert.Equal(JsonValueKind.Null, unassigned.GetProperty("assignedToName").ValueKind);
    }
}
