import type { createApiClient } from './client.ts';
import type { PagedResult } from '../types/api.ts';
import type { FileQuery, FileResponse, PersonalQuotaResponse } from '../types/file.ts';

/**
 * Tạo các thao tác đọc danh sách tệp và hạn mức lưu trữ cá nhân.
 * @param request Hàm gọi API đã cấu hình xác thực.
 * @returns Các hàm đọc dữ liệu tệp và quota.
 */
export function createFileReadApi(request: ReturnType<typeof createApiClient>) {
  return {
    /** Lấy trang tệp theo tham số phân trang và truyền tiếp tín hiệu hủy nếu có. */
    list(query: FileQuery = {}, signal?: AbortSignal) {
      const params = new URLSearchParams({
        page: String(query.page ?? 1),
        pageSize: String(query.pageSize ?? 20),
      });
      return request<PagedResult<FileResponse>>(`/files?${params}`, { signal });
    },
    /** Lấy hạn mức lưu trữ của người dùng hiện tại. */
    quota(signal?: AbortSignal) {
      return request<PersonalQuotaResponse>('/me/storage-quota', { signal });
    },
  };
}
