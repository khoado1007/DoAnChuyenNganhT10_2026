import type { ApiResponse } from '../types/api.ts';

export class ApiError extends Error {
  status: number;
  errors: Record<string, string[]> | null;

  /** Tạo lỗi API có mã HTTP và chi tiết lỗi theo từng trường nếu có. */
  constructor(message: string, status: number, errors: Record<string, string[]> | null = null) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.errors = errors;
  }
}

/**
 * Tạo hàm gọi API có xác thực, giải mã phản hồi và chuẩn hóa lỗi.
 * @param baseUrl Tiền tố URL của API.
 * @param getToken Hàm lấy token hiện hành.
 * @param onUnauthorized Callback xử lý khi token hiện hành bị từ chối.
 * @param fetcher Hàm gửi HTTP, mặc định dùng fetch của trình duyệt.
 * @returns Hàm request hỗ trợ phản hồi JSON hoặc blob.
 */
export function createApiClient(
  baseUrl: string,
  getToken: () => string | null,
  onUnauthorized: () => void,
  fetcher: typeof fetch = fetch,
) {
  // Gửi request theo kiểu dữ liệu yêu cầu, đồng thời xử lý token, hủy request và lỗi API.
  function request(path: string, init: RequestInit & { responseType: 'blob' }): Promise<Blob>;
  function request<T = void>(path: string, init?: RequestInit & { responseType?: 'json' }): Promise<T>;
  async function request<T>(path: string, init: RequestInit & { responseType?: 'json' | 'blob' } = {}): Promise<T | Blob> {
    const { responseType = 'json', ...fetchInit } = init;
    const headers = new Headers(init.headers);
    headers.set('Accept', responseType === 'blob' ? 'application/octet-stream' : 'application/json');
    if (init.body && !(init.body instanceof FormData)) headers.set('Content-Type', 'application/json');
    const token = getToken();
    if (token) headers.set('Authorization', `Bearer ${token}`);
    const response = await fetcher(`${baseUrl.replace(/\/$/, '')}${path}`, { ...fetchInit, headers });
    if (response.status === 401 && token && token === getToken() && !init.signal?.aborted) onUnauthorized();
    if (response.status === 204) {
      if (responseType === 'blob') throw new ApiError('Máy chủ không trả nội dung file.', response.status);
      return undefined as T;
    }
    if (response.ok && responseType === 'blob') return response.blob();
    let payload: ApiResponse<T>;
    try { payload = await response.json() as ApiResponse<T>; }
    catch { throw new ApiError('Không thể đọc phản hồi từ máy chủ. Vui lòng thử lại.', response.status); }
    if (!response.ok || !payload.success) {
      throw new ApiError(payload.message || 'Yêu cầu không thành công.', response.status, payload.errors);
    }
    return payload.data;
  }
  return request;
}

