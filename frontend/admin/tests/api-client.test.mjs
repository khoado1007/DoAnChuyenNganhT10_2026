import test from 'node:test';
import assert from 'node:assert/strict';

// Kiểm tra request gắn token xác thực và trả về phần data đã giải bọc.
test('API client attaches token and unwraps the response data', async () => {
  const { createApiClient } = await import('../src/api/client.ts');
  const request = createApiClient('/api', () => 'test-token', () => {}, async (url, init) => {
    assert.equal(url, '/api/auth/me');
    assert.equal(new Headers(init.headers).get('Authorization'), 'Bearer test-token');
    return Response.json({ success: true, message: '', data: { username: 'admin' }, errors: null });
  });
  assert.deepEqual(await request('/auth/me'), { username: 'admin' });
});

// Kiểm tra phản hồi 401 kích hoạt xử lý phiên và lỗi xác thực giữ nguyên chi tiết trường.
test('401 clears the session while validation errors preserve field details', async () => {
  const { createApiClient, ApiError } = await import('../src/api/client.ts');
  let invalidated = false;
  const unauthorized = createApiClient('/api', () => 'expired', () => { invalidated = true; }, async () =>
    Response.json({ success: false, message: 'Expired', data: null, errors: null }, { status: 401 }));
  await assert.rejects(unauthorized('/auth/me'), error => error instanceof ApiError && error.status === 401);
  assert.equal(invalidated, true);
  const invalid = createApiClient('/api', () => null, () => {}, async () =>
    Response.json({ success: false, message: 'Invalid', data: null, errors: { username: ['Required'] } }, { status: 400 }));
  await assert.rejects(invalid('/auth/login', { method: 'POST' }), error =>
    error instanceof ApiError && error.errors.username[0] === 'Required');
});

// Kiểm tra phản hồi máy chủ không phải JSON được chuyển thành lỗi API dễ đọc.
test('non-JSON server failures become readable API errors', async () => {
  const { createApiClient, ApiError } = await import('../src/api/client.ts');
  const request = createApiClient('/api', () => null, () => {}, async () => new Response('Bad gateway', { status: 502 }));
  await assert.rejects(request('/auth/me'), error => error instanceof ApiError && error.status === 502);
});

// Đảm bảo 401 của request cũ không xóa phiên đăng nhập bằng token mới hơn.
test('late 401 from an older token cannot invalidate a newer login', async () => {
  const { createApiClient } = await import('../src/api/client.ts');
  let token = 'old'; let invalidations = 0; let finish;
  const request = createApiClient('/api', () => token, () => invalidations++, () => new Promise(resolve => { finish = resolve; }));
  const pending = request('/auth/me'); token = 'new';
  finish(Response.json({ success: false, message: 'Expired' }, { status: 401 }));
  await assert.rejects(pending); assert.equal(invalidations, 0);
});

// Đảm bảo request bị hủy hoặc chưa xác thực không vô hiệu hóa phiên hợp lệ.
test('aborted and unauthenticated requests cannot clear an authenticated session', async () => {
  const { createApiClient } = await import('../src/api/client.ts');
  let invalidations = 0; const abort = new AbortController();
  const request = createApiClient('/api', () => 'valid', () => invalidations++, async () => {
    abort.abort(); return Response.json({ success: false }, { status: 401 });
  });
  await assert.rejects(request('/auth/me', { signal: abort.signal })); assert.equal(invalidations, 0);
  const anonymous = createApiClient('/api', () => null, () => invalidations++, async () => Response.json({ success: false }, { status: 401 }));
  await assert.rejects(anonymous('/auth/login')); assert.equal(invalidations, 0);
});

// Kiểm tra DELETE 204 thành công không cần JSON, còn phản hồi 200 rỗng vẫn bị báo lỗi.
test('204 deletion succeeds without a JSON body; empty 200 still fails', async () => {
  const { createApiClient, ApiError } = await import('../src/api/client.ts');
  const request = createApiClient('/api', () => 'token', () => {}, async (_url, init) => {
    assert.equal(init.method, 'DELETE');
    return new Response(null, { status: 204 });
  });
  assert.equal(await request('/files/id', { method: 'DELETE' }), undefined);
  const malformed = createApiClient('/api', () => 'token', () => {}, async () => new Response('', { status: 200 }));
  await assert.rejects(malformed('/files'), error => error instanceof ApiError && error.status === 200);
});

// Kiểm tra tải blob giữ nguyên byte, token và tín hiệu hủy mà không lộ tùy chọn nội bộ.
test('blob download preserves bytes, auth and abort signal without sending responseType to fetch', async () => {
  const { createApiClient } = await import('../src/api/client.ts');
  const bytes = new Uint8Array([0, 255, 128, 10]); const abort = new AbortController();
  const request = createApiClient('/api/', () => 'token', () => {}, async (url, init) => {
    assert.equal(url, '/api/files/id/download');
    assert.equal(new Headers(init.headers).get('Authorization'), 'Bearer token');
    assert.equal(new Headers(init.headers).get('Accept'), 'application/octet-stream');
    assert.equal(init.signal, abort.signal);
    assert.equal('responseType' in init, false);
    return new Response(bytes, { headers: { 'Content-Type': 'application/octet-stream' } });
  });
  const blob = await request('/files/id/download', { responseType: 'blob', signal: abort.signal });
  assert.ok(blob instanceof Blob); assert.deepEqual(new Uint8Array(await blob.arrayBuffer()), bytes);
});

// Đảm bảo lỗi tải blob giữ mã API và nội dung tải rỗng bị từ chối.
test('blob errors stay API errors and an empty download is rejected', async () => {
  const { createApiClient, ApiError } = await import('../src/api/client.ts');
  const missing = createApiClient('/api', () => 'token', () => {}, async () =>
    Response.json({ success: false, message: 'File missing', errors: null }, { status: 404 }));
  await assert.rejects(missing('/files/id/download', { responseType: 'blob' }), e => e instanceof ApiError && e.status === 404 && e.message === 'File missing');
  const empty = createApiClient('/api', () => 'token', () => {}, async () => new Response(null, { status: 204 }));
  await assert.rejects(empty('/files/id/download', { responseType: 'blob' }), e => e instanceof ApiError && e.status === 204);
  const proxy = createApiClient('/api', () => 'token', () => {}, async () => new Response('Bad gateway', { status: 502 }));
  await assert.rejects(proxy('/files/id/download', { responseType: 'blob' }), e => e instanceof ApiError && e.status === 502);
});

// Kiểm tra 401 khi tải blob chỉ vô hiệu hóa phiên chứa token gây lỗi.
test('blob 401 invalidates current session but not a newer login', async () => {
  const { createApiClient } = await import('../src/api/client.ts');
  let token = 'old'; let invalidations = 0; let finish;
  const request = createApiClient('/api', () => token, () => invalidations++, () => new Promise(resolve => { finish = resolve; }));
  const old = request('/download', { responseType: 'blob' }); token = 'new';
  finish(Response.json({ success: false }, { status: 401 })); await assert.rejects(old); assert.equal(invalidations, 0);
  const current = request('/download', { responseType: 'blob' });
  finish(Response.json({ success: false }, { status: 401 })); await assert.rejects(current); assert.equal(invalidations, 1);
});
