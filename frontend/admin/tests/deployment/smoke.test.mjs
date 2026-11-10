import test from 'node:test';
import assert from 'node:assert/strict';

// Run explicitly against the local Docker stack, not as part of unit tests.
const baseUrl = process.env.WEB_BASE_URL || 'http://localhost:8088';
// Gửi request tới frontend đang chạy và tự giới hạn thời gian chờ.
const request = (path, options = {}) => fetch(new URL(path, baseUrl), {
  ...options,
  signal: AbortSignal.timeout(10000),
});

// Kiểm tra đường dẫn sâu trả HTML ứng dụng và mọi tài nguyên JS/CSS tải được.
test('React deep links load HTML and root-relative JavaScript/CSS', async () => {
  for (const path of ['/', '/admin/']) {
    const response = await request(path);
    assert.equal(response.status, 200);
    assert.match(response.headers.get('content-type'), /text\/html/);
    const html = await response.text();
    assert.match(html, /id="root"/);
    const assets = [...html.matchAll(/(?:src|href)="(\/assets\/[^" ]+)"/g)];
    assert.ok(assets.length >= 2, 'Expected root-relative JS and CSS links');
    for (const [, asset] of assets) {
      const resource = await request(asset);
      assert.equal(resource.status, 200, asset);
      assert.doesNotMatch(resource.headers.get('content-type'), /text\/html/);
    }
  }
});

// Đảm bảo API hoặc tài nguyên không tồn tại trả 404 thay vì trang React dự phòng.
test('unknown API and static asset paths never return the React page', async () => {
  for (const path of ['/api', '/api/does-not-exist', '/assets/missing.js']) {
    const response = await request(path);
    assert.equal(response.status, 404, path);
    assert.doesNotMatch(await response.text(), /id="root"/);
  }
});

// Kiểm tra proxy giữ nguyên phản hồi JSON 401 cho request chưa xác thực.
test('reverse proxy preserves anonymous 401 JSON response', async () => {
  const response = await request('/api/auth/me');
  assert.equal(response.status, 401);
  assert.equal((await response.json()).success, false);
});

// Kiểm tra đăng nhập và đọc hồ sơ xác thực thành công qua cùng origin frontend.
test('login and authenticated profile work through the frontend origin', async () => {
  const response = await request('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      username: process.env.SMOKE_USERNAME || 'admin',
      password: process.env.SMOKE_PASSWORD || 'Admin@12345',
    }),
  });
  assert.equal(response.status, 200);
  const login = await response.json();
  assert.equal(login.success, true);
  assert.ok(login.data.accessToken);
  const profile = await request('/api/auth/me', {
    headers: { Authorization: `Bearer ${login.data.accessToken}` },
  });
  assert.equal(profile.status, 200);
  const user = await profile.json();
  assert.equal(user.success, true);
  assert.equal(user.data.username, login.data.user.username);
  assert.equal(user.data.passwordHash, undefined);
});
