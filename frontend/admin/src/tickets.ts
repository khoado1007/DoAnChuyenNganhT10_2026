export type TicketStatus = 'Open' | 'Assigned' | 'InProgress' | 'Resolved' | 'Closed' | 'Cancelled';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type TicketCategory = 'Hardware' | 'Software' | 'Network' | 'Account' | 'Storage' | 'Other';

export interface TicketListItem {
  id: string;
  ticketNumber: string;
  title: string;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  createdByName: string;
  assignedToName?: string | null;
  createdAt: string;
  updatedAt: string | null;
}

export interface TicketComment {
  id: string;
  authorUsername: string;
  content: string;
  isInternal: boolean;
  createdAt: string;
}

export interface TicketHistory {
  fromStatus: TicketStatus;
  toStatus: TicketStatus;
  changedByUsername: string;
  note: string | null;
  changedAt: string;
}

export interface TicketDetail extends TicketListItem {
  description: string;
  createdByUserId: string;
  assignedToUserId?: string | null;
  assignedToName: string | null;
  resolvedAt: string | null;
  resolution: string | null;
  comments: TicketComment[];
  history: TicketHistory[];
}

export const TICKET_STATUSES: TicketStatus[] = ['Open', 'InProgress', 'Resolved', 'Closed'];

const statusLabels: Record<TicketStatus, string> = {
  Open: 'Đang mở',
  Assigned: 'Đã phân công',
  InProgress: 'Đang xử lý',
  Resolved: 'Đã xử lý',
  Closed: 'Đã đóng',
  Cancelled: 'Đã hủy',
};

const statusColors: Record<TicketStatus, string> = {
  Open: 'gold',
  Assigned: 'cyan',
  InProgress: 'blue',
  Resolved: 'green',
  Closed: 'default',
  Cancelled: 'red',
};

const priorityLabels: Record<TicketPriority, string> = {
  Low: 'Thấp',
  Medium: 'Trung bình',
  High: 'Cao',
  Critical: 'Khẩn cấp',
};

const priorityColors: Record<TicketPriority, string> = {
  Low: 'default',
  Medium: 'blue',
  High: 'orange',
  Critical: 'red',
};

const categoryLabels: Record<TicketCategory, string> = {
  Hardware: 'Phần cứng',
  Software: 'Phần mềm',
  Network: 'Mạng',
  Account: 'Tài khoản',
  Storage: 'Lưu trữ',
  Other: 'Khác',
};

/** Nhãn tiếng Việt của trạng thái ticket. */
export const statusLabel = (status: TicketStatus) => statusLabels[status] ?? status;
/** Màu Tag Ant Design theo trạng thái. */
export const statusColor = (status: TicketStatus) => statusColors[status] ?? 'default';
export const priorityLabel = (priority: TicketPriority) => priorityLabels[priority] ?? priority;
export const priorityColor = (priority: TicketPriority) => priorityColors[priority] ?? 'default';
export const categoryLabel = (category: TicketCategory) => categoryLabels[category] ?? category;

/** Bước chuyển trạng thái hợp lệ tiếp theo; Admin là người thực hiện. */
export function nextTicketAction(status: TicketStatus): { status: TicketStatus; label: string; needsResolution: boolean } | null {
  switch (status) {
    case 'Open': return { status: 'InProgress', label: 'Bắt đầu xử lý', needsResolution: false };
    case 'InProgress': return { status: 'Resolved', label: 'Hoàn tất xử lý', needsResolution: true };
    case 'Resolved': return { status: 'Closed', label: 'Đóng ticket', needsResolution: false };
    default: return null;
  }
}

/** Chuẩn hóa thời điểm API về chuỗi UTC có hậu tố Z trước khi hiển thị. */
export function asUtcIso(value: string): string {
  return value.endsWith('Z') || value.includes('+') ? value : value + 'Z';
}

export const formatTicketTime = (value: string) => new Date(asUtcIso(value)).toLocaleString('vi-VN');

/** Lọc danh sách ticket tại client chỉ dùng cho demo; server vẫn là nguồn kiểm soát chính. */
export function matchesQuery(ticket: TicketListItem, query: { search?: string; status?: TicketStatus | ''; category?: TicketCategory | ''; priority?: TicketPriority | '' }) {
  const search = query.search?.trim().toLocaleLowerCase('vi') ?? '';
  if (search && !`${ticket.ticketNumber} ${ticket.title} ${ticket.createdByName}`.toLocaleLowerCase('vi').includes(search)) return false;
  if (query.status && ticket.status !== query.status) return false;
  if (query.category && ticket.category !== query.category) return false;
  if (query.priority && ticket.priority !== query.priority) return false;
  return true;
}
