import type { UserRole } from './types/auth';
export type AdminSection = 'overview' | 'users' | 'assets' | 'agents' | 'quotas' | 'tickets';
export type Route = { kind: 'admin'; section: AdminSection } | { kind: 'portal' | 'forbidden' | 'not-found' };
/** Trả về đường dẫn trang chủ tương ứng với vai trò người dùng. */
export const homeForRole = (role: UserRole) => '/' + role.toLowerCase();
/**
 * Phân giải đường dẫn và kiểm tra quyền truy cập theo vai trò.
 * @param path Đường dẫn cần kiểm tra.
 * @param role Vai trò hiện tại.
 * @returns Loại trang hợp lệ, bị cấm hoặc không tồn tại.
 */
export function resolveRoute(path: string, role: UserRole): Route {
  const normalized = path.replace(/\/+$/, '') || '/';
  const segments = normalized.split('/').filter(Boolean);
  const group = segments[0];
  if (!['admin', 'employee', 'technician'].includes(group)) return { kind: 'not-found' };
  if ('/' + group !== homeForRole(role)) return { kind: 'forbidden' };
  if (role !== 'Admin') return { kind: segments.length === 1 ? 'portal' : 'not-found' };
  const section = segments[1] || 'overview';
  if (segments.length > 2 || !['overview', 'users', 'assets', 'agents', 'quotas', 'tickets'].includes(section)) return { kind: 'not-found' };
  return { kind: 'admin', section: section as AdminSection };
}

/**
 * Chọn điểm đến sau đăng nhập, chỉ tiếp tục đường dẫn người dùng được phép truy cập.
 * @param path Đường dẫn ban đầu.
 * @param role Vai trò của người dùng vừa đăng nhập.
 * @returns Đường dẫn hợp lệ hoặc trang chủ của vai trò.
 */
export function destinationAfterLogin(path: string, role: UserRole): string {
  const route = resolveRoute(path, role);
  return route.kind === 'admin' || route.kind === 'portal' ? path : homeForRole(role);
}
