import test from 'node:test';
import assert from 'node:assert/strict';
import { createApiClient, ApiError } from '../src/api/client.ts';

// Kiểm tra upload gửi đúng một tệp multipart kèm token/tín hiệu hủy mà không tự đặt Content-Type.
test('upload sends one multipart file with auth and cancellation, without forcing content type', async () => {
  const { createFileUploadApi } = await import('../src/api/file-upload.ts');
  const file = new File([new Uint8Array([0, 255, 42])], 'báo cáo.bin');
  const abort = new AbortController();
  const request = createApiClient('/api', () => 'token', () => {}, async (url, init) => {
    assert.equal(url, '/api/files'); assert.equal(init.method, 'POST'); assert.equal(init.signal, abort.signal);
    const headers = new Headers(init.headers);
    assert.equal(headers.get('Authorization'), 'Bearer token'); assert.equal(headers.has('Content-Type'), false);
    assert.ok(init.body instanceof FormData); assert.deepEqual([...init.body.keys()], ['file']);
    const part = init.body.get('file'); assert.equal(part.name, file.name);
    assert.deepEqual(new Uint8Array(await part.arrayBuffer()), new Uint8Array([0, 255, 42]));
    return Response.json({ success: true, data: { id: 'saved', sizeBytes: 3 } }, { status: 201 });
  });
  assert.deepEqual(await createFileUploadApi(request).upload(file, abort.signal), { id: 'saved', sizeBytes: 3 });
});

// Đảm bảo lỗi vượt quota và hủy request được truyền lên, không tự thử lại.
test('upload propagates quota rejection and cancellation without retrying', async () => {
  const { createFileUploadApi } = await import('../src/api/file-upload.ts');
  let calls = 0;
  const denied = createApiClient('/api', () => 'token', () => {}, async () => {
    calls++; return Response.json({ success: false, message: 'Quota exceeded' }, { status: 413 });
  });
  const file = new File(['data'], 'a.txt');
  await assert.rejects(createFileUploadApi(denied).upload(file), e => e instanceof ApiError && e.status === 413);
  assert.equal(calls, 1);
  const abort = new AbortController(); abort.abort();
  const cancelled = createApiClient('/api', () => 'token', () => {}, async (_url, init) => { init.signal.throwIfAborted(); });
  await assert.rejects(createFileUploadApi(cancelled).upload(file, abort.signal), e => e.name === 'AbortError');
});
