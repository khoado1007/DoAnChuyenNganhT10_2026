import { createDeviceSessionStore } from './device-session';
import { createApiClient } from './client';
import type { AuthResponse, LoginRequest, UserResponse } from '../types/auth';
import { isDemoMode } from '../demo/mode';
import { createDemoFetch } from '../demo/server';

const tokenKey = isDemoMode ? 'itdesk.previewAccessToken' : 'itdesk.accessToken';
export const deviceSessions = createDeviceSessionStore(sessionStorage, isDemoMode ? 'itdesk.previewDeviceSession' : 'itdesk.deviceSession');
export const sessionExpiredEvent = 'itdesk:session-expired';
/** Đọc access token đang lưu trong session của tab hiện tại. */
export const getToken = () => sessionStorage.getItem(tokenKey);
/** Xóa access token để kết thúc phiên đăng nhập cục bộ. */
export const clearSession = () => { sessionStorage.removeItem(tokenKey); deviceSessions.clear(); };
/** API dùng token hiện tại và phát sự kiện khi phiên bị máy chủ từ chối. */
export const api = createApiClient(import.meta.env.VITE_API_BASE_URL || '/api', getToken, () => {
  clearSession();
  window.dispatchEvent(new Event(sessionExpiredEvent));
}, isDemoMode ? createDemoFetch() : fetch);

/**
 * Đăng nhập bằng thông tin xác thực và lưu token nhận được vào session.
 * @param values Tên đăng nhập và mật khẩu do người dùng cung cấp.
 * @returns Hồ sơ người dùng sau khi đăng nhập thành công.
 */
export async function login(values: LoginRequest): Promise<UserResponse> {
  const result = await api<AuthResponse>('/auth/login', { method: 'POST', body: JSON.stringify(values) });
  deviceSessions.clear();
  sessionStorage.setItem(tokenKey, result.accessToken);
  return result.user;
}

export function endMachineSession(saved: { deviceId: string; sessionId: string }) {
  return api(`/machines/${encodeURIComponent(saved.deviceId)}/logout`, {
    method: 'POST', headers: { 'X-Device-Session': saved.sessionId },
  });
}

/** Thứ tự trong tab bảo vệ phiên khi tín hiệu rời trang đến sau yêu cầu khôi phục. */
export async function reportMachinePage(leaving: boolean) {
  const saved = deviceSessions.read();
  if (!saved || !getToken() || isDemoMode) return;
  const key = 'itdesk.machinePageSequence';
  const sequence = Number(sessionStorage.getItem(key) || 0) + 1;
  sessionStorage.setItem(key, String(sequence));
  await api(`/machines/${encodeURIComponent(saved.deviceId)}/page`, {
    method: 'POST', keepalive: leaving, headers: { 'X-Device-Session': saved.sessionId },
    body: JSON.stringify({ sequence, leaving }),
  });
}

/** Lấy hồ sơ của phiên hiện tại; cho phép hủy yêu cầu khi component bị tháo. */
export const getCurrentUser = (signal?: AbortSignal) => api<UserResponse>('/auth/me', { signal });
