using ITManagement.Application.Common.Interfaces;
using ITManagement.Application.Common.Models;
using ITManagement.Application.Contracts.Tickets;
using ITManagement.Application.Services;
using ITManagement.Domain.Entities.Ticket;
using ITManagement.Domain.Enums;
using ITManagement.Domain.Exceptions;
using ITManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ITManagement.Infrastructure.Services;

/// <summary>
/// Ticket cơ bản: Employee tạo và theo dõi ticket của mình; Admin xử lý toàn bộ.
/// Technician chưa có giao diện riêng nhưng phạm vi dữ liệu đã giới hạn theo ticket được giao.
/// </summary>
public sealed class TicketService(AppDbContext db, ICurrentUserService current, TimeProvider clock) : ITicketService
{
    private static readonly TicketStatus[] SupportedStatuses =
        [TicketStatus.Open, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed];

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private string Actor => current.Username ?? "system";

    public async Task<PagedResult<TicketListItemResponse>> ListAsync(TicketQuery query, CancellationToken ct)
    {
        var (userId, role) = RequireUser();
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var q = Scope(db.Tickets.AsNoTracking(), userId, role);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(t => t.Title.Contains(term) || t.TicketNumber.Contains(term));
        }
        if (query.Status.HasValue)
            q = q.Where(t => t.Status == query.Status.Value);
        if (query.Category.HasValue)
            q = q.Where(t => t.Category == query.Category.Value);
        if (query.Priority.HasValue)
            q = q.Where(t => t.Priority == query.Priority.Value);

        var total = await q.CountAsync(ct);
        var rows = await q
            .OrderByDescending(t => t.CreatedAt)
            .ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.TicketNumber,
                t.Title,
                t.Category,
                t.Priority,
                t.Status,
                CreatedByName = t.CreatedBy.FullName,
                AssignedToName = t.AssignedTo != null ? t.AssignedTo.FullName : null,
                t.CreatedAt,
                t.UpdatedAt
            })
            .ToListAsync(ct);

        var items = rows.Select(t => new TicketListItemResponse(
            t.Id, t.TicketNumber, t.Title, t.Category, t.Priority, t.Status,
            t.CreatedByName, t.AssignedToName, Utc(t.CreatedAt), Utc(t.UpdatedAt))).ToList();

        return PagedResult<TicketListItemResponse>.Create(items, page, pageSize, total);
    }

    public async Task<TicketDetailResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var (userId, role) = RequireUser();
        return await BuildDetailAsync(id, userId, role, ct);
    }

    public async Task<TicketDetailResponse> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        var (userId, role) = RequireUser();
        if (role != UserRole.Employee)
            throw Forbidden("Chỉ nhân viên được gửi yêu cầu hỗ trợ.");

        var title = request.Title?.Trim() ?? string.Empty;
        var description = request.Description?.Trim() ?? string.Empty;
        if (title.Length < 5 || description.Length < 10)
            throw Invalid("TICKET_INVALID_CONTENT", "Tiêu đề tối thiểu 5 ký tự và mô tả tối thiểu 10 ký tự.");
        if (!Enum.IsDefined(request.Category) || !Enum.IsDefined(request.Priority))
            throw Invalid("TICKET_INVALID_CATEGORY", "Danh mục hoặc mức ưu tiên không hợp lệ.");

        // Sequence cấp số nguyên tử; năm chỉ là tiền tố hiển thị, không reset bộ đếm.
        var sequence = (await db.Database
            .SqlQueryRaw<int>("SELECT NEXT VALUE FOR [ticket_number_seq] AS [Value]")
            .ToListAsync(ct)).Single();
        var ticket = new Ticket
        {
            TicketNumber = $"TKT-{Now.Year}-{sequence:D4}",
            Title = title,
            Description = description,
            Category = request.Category,
            Priority = request.Priority,
            Status = TicketStatus.Open,
            CreatedByUserId = userId,
            CreatedAt = Now
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync(ct);

        return await BuildDetailAsync(ticket.Id, userId, role, ct);
    }

    public async Task<TicketDetailResponse> UpdateStatusAsync(Guid id, UpdateTicketStatusRequest request, CancellationToken ct)
    {
        var (userId, role) = RequireUser();
        if (role != UserRole.Admin && role != UserRole.Technician)
            throw Forbidden("Chỉ quản trị viên hoặc kỹ thuật viên được giao mới được cập nhật trạng thái yêu cầu hỗ trợ.");
        if (!SupportedStatuses.Contains(request.Status))
            throw Invalid("TICKET_STATUS_UNSUPPORTED", "Chỉ hỗ trợ trạng thái Đang mở, Đang xử lý, Đã xử lý và Đã đóng.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ticket = await db.Tickets
            .FromSqlInterpolated($"SELECT * FROM tickets WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}")
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Ticket", id);

        if (role == UserRole.Technician && ticket.AssignedToUserId != userId)
            throw Forbidden("Chỉ kỹ thuật viên được phân công mới có quyền cập nhật ticket này.");

        var next = NextStatus(ticket.Status);
        if (next != request.Status)
            throw Invalid("TICKET_STATUS_TRANSITION",
                $"Không thể chuyển từ {StatusLabel(ticket.Status)} sang {StatusLabel(request.Status)}.");

        var resolution = request.Resolution?.Trim();
        if (request.Status == TicketStatus.Resolved && string.IsNullOrWhiteSpace(resolution))
            throw Invalid("TICKET_RESOLUTION_REQUIRED", "Vui lòng nhập kết quả xử lý trước khi hoàn tất ticket.");

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        db.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            FromStatus = ticket.Status,
            ToStatus = request.Status,
            ChangedByUsername = Actor,
            Note = note,
            ChangedAt = Now,
            CreatedAt = Now
        });

        ticket.Status = request.Status;
        ticket.UpdatedAt = Now;
        ticket.UpdatedBy = Actor;
        if (request.Status == TicketStatus.Resolved)
        {
            ticket.Resolution = resolution;
            ticket.ResolvedAt = Now;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await BuildDetailAsync(id, userId, role, ct);
    }

    public async Task<TicketDetailResponse> AssignAsync(Guid id, AssignTicketRequest request, CancellationToken ct)
    {
        var (userId, role) = RequireUser();
        if (role != UserRole.Admin)
            throw Forbidden("Chỉ quản trị viên được phân công yêu cầu hỗ trợ.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var ticket = await db.Tickets
            .FromSqlInterpolated($"SELECT * FROM tickets WITH (UPDLOCK, ROWLOCK) WHERE \"Id\" = {id}")
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Ticket", id);

        string historyNote;
        if (request.AssignedToUserId.HasValue)
        {
            var tech = await db.AppUsers.AsNoTracking()
                .SingleOrDefaultAsync(u => u.Id == request.AssignedToUserId.Value, ct);
            if (tech is null || tech.Status != UserStatus.Active || tech.Role != UserRole.Technician)
                throw Invalid("TICKET_INVALID_ASSIGNEE", "Kỹ thuật viên không tồn tại hoặc không ở trạng thái hoạt động.");

            ticket.AssignedToUserId = tech.Id;
            var noteExtra = string.IsNullOrWhiteSpace(request.Note) ? "" : $" Ghi chú: {request.Note.Trim()}";
            historyNote = $"Phân công cho {tech.FullName} ({tech.Username}).{noteExtra}";
        }
        else
        {
            ticket.AssignedToUserId = null;
            var noteExtra = string.IsNullOrWhiteSpace(request.Note) ? "" : $" Ghi chú: {request.Note.Trim()}";
            historyNote = $"Hủy phân công.{noteExtra}";
        }

        db.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticket.Id,
            FromStatus = ticket.Status,
            ToStatus = ticket.Status,
            ChangedByUsername = Actor,
            Note = historyNote,
            ChangedAt = Now,
            CreatedAt = Now
        });

        ticket.UpdatedAt = Now;
        ticket.UpdatedBy = Actor;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await BuildDetailAsync(id, userId, role, ct);
    }

    public async Task<TicketCommentResponse> AddCommentAsync(Guid id, AddTicketCommentRequest request, CancellationToken ct)
    {
        var (userId, role) = RequireUser();
        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Ticket", id);
        if (!CanView(ticket, userId, role))
            throw new NotFoundException("Ticket", id);

        var content = request.Content?.Trim() ?? string.Empty;
        if (content.Length == 0)
            throw Invalid("TICKET_COMMENT_REQUIRED", "Nội dung bình luận không được để trống.");
        if (request.IsInternal && role == UserRole.Employee)
            throw Invalid("TICKET_INTERNAL_FORBIDDEN", "Nhân viên không được tạo ghi chú nội bộ.");

        var comment = new TicketComment
        {
            TicketId = id,
            AuthorUsername = Actor,
            Content = content,
            IsInternal = request.IsInternal && role != UserRole.Employee,
            CreatedAt = Now
        };
        db.TicketComments.Add(comment);
        ticket.UpdatedAt = Now;
        ticket.UpdatedBy = Actor;
        await db.SaveChangesAsync(ct);

        return new TicketCommentResponse(comment.Id, comment.AuthorUsername, comment.Content,
            comment.IsInternal, Utc(comment.CreatedAt));
    }

    private static IQueryable<Ticket> Scope(IQueryable<Ticket> q, Guid userId, UserRole role) => role switch
    {
        UserRole.Admin => q,
        UserRole.Technician => q.Where(t => t.AssignedToUserId == userId),
        _ => q.Where(t => t.CreatedByUserId == userId)
    };

    private static bool CanView(Ticket ticket, Guid userId, UserRole role) => role switch
    {
        UserRole.Admin => true,
        UserRole.Technician => ticket.AssignedToUserId == userId,
        _ => ticket.CreatedByUserId == userId
    };

    private static TicketStatus? NextStatus(TicketStatus current) => current switch
    {
        TicketStatus.Open => TicketStatus.InProgress,
        TicketStatus.InProgress => TicketStatus.Resolved,
        TicketStatus.Resolved => TicketStatus.Closed,
        _ => null
    };

    private async Task<TicketDetailResponse> BuildDetailAsync(Guid id, Guid userId, UserRole role, CancellationToken ct)
    {
        var ticket = await db.Tickets.AsNoTracking()
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new NotFoundException("Ticket", id);
        if (!CanView(ticket, userId, role))
            throw new NotFoundException("Ticket", id);

        var commentsQuery = db.TicketComments.AsNoTracking().Where(c => c.TicketId == id);
        if (role == UserRole.Employee)
            commentsQuery = commentsQuery.Where(c => !c.IsInternal);
        var comments = await commentsQuery.OrderBy(c => c.CreatedAt).ThenBy(c => c.Id).ToListAsync(ct);

        var history = await db.TicketHistories.AsNoTracking()
            .Where(h => h.TicketId == id)
            .OrderBy(h => h.ChangedAt)
            .ThenBy(h => h.Id)
            .ToListAsync(ct);

        return new TicketDetailResponse(
            ticket.Id,
            ticket.TicketNumber,
            ticket.Title,
            ticket.Description,
            ticket.Category,
            ticket.Priority,
            ticket.Status,
            ticket.CreatedByUserId,
            ticket.CreatedBy.FullName,
            ticket.AssignedToUserId,
            ticket.AssignedTo?.FullName,
            Utc(ticket.CreatedAt),
            Utc(ticket.UpdatedAt),
            Utc(ticket.ResolvedAt),
            ticket.Resolution,
            comments.Select(c => new TicketCommentResponse(c.Id, c.AuthorUsername, c.Content,
                c.IsInternal, Utc(c.CreatedAt))).ToList(),
            history.Select(h => new TicketHistoryResponse(h.FromStatus, h.ToStatus,
                h.ChangedByUsername, h.Note, Utc(h.ChangedAt))).ToList());
    }

    private (Guid UserId, UserRole Role) RequireUser()
    {
        if (current.UserId is not Guid userId || current.Role is not UserRole role)
            throw new SimulationException(401, "UNAUTHENTICATED", "Vui lòng đăng nhập.");
        return (userId, role);
    }

    private static SimulationException Forbidden(string message) =>
        new(403, "TICKET_FORBIDDEN", message);

    private static SimulationException Invalid(string code, string message) =>
        new(400, code, message);

    private static string StatusLabel(TicketStatus status) => status switch
    {
        TicketStatus.Open => "Đang mở",
        TicketStatus.InProgress => "Đang xử lý",
        TicketStatus.Resolved => "Đã xử lý",
        TicketStatus.Closed => "Đã đóng",
        _ => status.ToString()
    };

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
    private static DateTime? Utc(DateTime? value) => value.HasValue
        ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        : null;
}
