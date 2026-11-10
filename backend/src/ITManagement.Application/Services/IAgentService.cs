using ITManagement.Application.Contracts.Agents;
namespace ITManagement.Application.Services;

public interface IAgentService
{
    /// <summary>Cấp token đăng ký agent.</summary>
    Task<EnrollmentIssued> IssueAsync(IssueEnrollmentRequest request, CancellationToken ct);
    /// <summary>Cấp token khôi phục đăng ký agent.</summary>
    Task<EnrollmentIssued> IssueRecoveryAsync(IssueRecoveryRequest request, CancellationToken ct);
    /// <summary>Đăng ký agent bằng token hợp lệ.</summary>
    Task<AgentEnrolled> EnrollAsync(EnrollRequest request, CancellationToken ct);
    /// <summary>Xác thực agent và tiếp nhận heartbeat.</summary>
    Task<HeartbeatAccepted> HeartbeatAsync(string credential, HeartbeatRequest request, CancellationToken ct);
    /// <summary>Liệt kê các agent đã đăng ký.</summary>
    Task<IReadOnlyList<AgentResponse>> ListAsync(CancellationToken ct);
    /// <summary>Vô hiệu hóa agent theo mã định danh.</summary>
    Task DisableAsync(Guid id, CancellationToken ct);
}
