import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiClient, ApiError } from '../src/api/client.ts';

// Kiểm tra danh sách tệp dùng phân trang, xác thực và chuyển tiếp tín hiệu hủy.
test('file list uses pagination and authenticated client, forwarding cancellation', async () => {
  const { createFileReadApi } = await import('../src/api/file-read.ts');
  const abort = new AbortController(); const paths = [];
  const page = { items: [], page: 2, pageSize: 5, totalCount: 0, totalPages: 0, hasPreviousPage: true, hasNextPage: false };
  const request = createApiClient('/api', () => 'token', () => {}, async (url, init) => {
    paths.push(url); assert.equal(init.signal, abort.signal);
    assert.equal(new Headers(init.headers).get('Authorization'), 'Bearer token');
    return Response.json({ success: true, data: page });
  });
  const files = createFileReadApi(request);
  assert.deepEqual(await files.list({ page: 2, pageSize: 5 }, abort.signal), page);
  await files.list(undefined, abort.signal);
  assert.deepEqual(paths, ['/api/files?page=2&pageSize=5', '/api/files?page=1&pageSize=20']);
});

// Kiểm tra quota lấy theo người dùng hiện tại và giữ nguyên lỗi API trả về.
test('quota reads current user route without owner parameters and preserves API errors', async () => {
  const { createFileReadApi } = await import('../src/api/file-read.ts');
  const abort = new AbortController();
  const quota = { quotaBytes: 10, usedBytes: 4, availableBytes: 6, maxFileBytes: 10485760 };
  const request = createApiClient('/api', () => 'token', () => {}, async (url, init) => {
    assert.equal(url, '/api/me/storage-quota'); assert.equal(init.signal, abort.signal);
    return Response.json({ success: true, data: quota });
  });
  assert.deepEqual(await createFileReadApi(request).quota(abort.signal), quota);
  const failure = createApiClient('/api', () => 'token', () => {}, async () => Response.json({ success: false, message: 'Forbidden' }, { status: 403 }));
  await assert.rejects(createFileReadApi(failure).quota(), e => e instanceof ApiError && e.status === 403);
});
