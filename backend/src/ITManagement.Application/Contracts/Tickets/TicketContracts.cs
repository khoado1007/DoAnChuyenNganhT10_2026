using System.ComponentModel.DataAnnotations;
using ITManagement.Domain.Enums;

namespace ITManagement.Application.Contracts.Tickets;

public sealed record CreateTicketRequest(
    [Required, StringLength(500)] string Title,
    [Required, StringLength(4000)] string Description,
    [EnumDataType(typeof(TicketCategory))] TicketCategory Category,
    [EnumDataType(typeof(TicketPriority))] TicketPriority Priority);

public sealed record TicketResponse(Guid Id, string TicketNumber, string Title, string Description,
    TicketCategory Category, TicketPriority Priority, TicketStatus Status,
    Guid CreatedByUserId, string CreatedByName, Guid? AssignedToUserId, string? AssignedToName,
    DateTime CreatedAt, DateTime? UpdatedAt);
