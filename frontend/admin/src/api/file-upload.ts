import type { createApiClient } from './client.ts';
import type { FileResponse } from '../types/file.ts';

/**
 * Tạo thao tác tải tệp lên bằng multipart/form-data.
 * @param request Hàm gọi API đã cấu hình xác thực.
 * @returns Hàm tải một tệp lên và nhận thông tin tệp đã lưu.
 */
export function createFileUploadApi(request: ReturnType<typeof createApiClient>) {
  return {
    /** Gửi một tệp và chuyển tiếp tín hiệu hủy tới request. */
    upload(file: File, signal?: AbortSignal) {
      const body = new FormData();
      body.append('file', file, file.name);
      return request<FileResponse>('/files', { method: 'POST', body, signal });
    },
  };
}
