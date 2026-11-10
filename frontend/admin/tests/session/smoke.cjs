// Production UI smoke with controlled API responses; no business database required.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const base = process.env.ITAM_UI_URL || 'http://127.0.0.1:5191';
// Kiểm thử khôi phục phiên production, giữ/xóa token theo mã lỗi và chỉ tải trang cần thiết.
(async () => {
 const browser = await chromium.launch({ channel: 'msedge', headless: true });
 try {
  const page = await browser.newPage(); const scripts = []; let meStatus = 200; let meCalls = 0;
  const user = { id: 'test-admin', username: 'admin', fullName: 'Test Admin', email: 'admin@test.local', department: 'IT', role: 'Admin', status: 'Active' };
  page.on('request', req => { if (req.resourceType() === 'script') scripts.push(req.url()); });
  // Giả lập các endpoint đăng nhập/hồ sơ và cho phép thay đổi mã lỗi giữa các kịch bản.
  await page.route('**/api/**', async route => {
   const path = new URL(route.request().url()).pathname;
   let data = null; let status = 200;
   if (path === '/api/auth/login') data = { user, accessToken: 'ui-test-token' };
   else if (path === '/api/auth/me') { meCalls++; status = meStatus; data = user; }
   else if (path === '/api/users') data = { items: [], totalCount: 0, page: 1, pageSize: 10 };
   else throw new Error('Unexpected API call: ' + path);
   await route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ success: status === 200, data, message: status === 200 ? '' : 'Test unavailable' }) });
  });
  await page.goto(base + '/admin/users');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).waitFor();
  assert.equal(scripts.some(url => /(?:AdminPreviewPage|UsersPage|AgentsPage|AssetsPage|PortalPage|Table)-/.test(url)), false, 'Login must not download protected page/table chunks');
  await page.getByLabel('Tên đăng nhập', { exact: true }).fill('admin');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('test-password');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();
  await page.getByRole('heading', { name: 'Tài khoản', exact: true }).waitFor();
  assert.equal(new URL(page.url()).pathname, '/admin/users');
  assert.equal(scripts.some(url => /(?:AgentsPage|AssetsPage|PortalPage)-/.test(url)), false, 'Unused page chunks must remain unloaded');
  meCalls = 0;
  await page.reload(); await page.getByRole('heading', { name: 'Tài khoản', exact: true }).waitFor();
  assert.equal(meCalls, 1, 'Production refresh restores session once');
  assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), 'ui-test-token');
  meStatus = 503;
  await page.reload(); await page.getByRole('button', { name: 'Thử lại', exact: true }).waitFor();
  assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), 'ui-test-token');
  meStatus = 200; await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
  await page.getByRole('heading', { name: 'Tài khoản', exact: true }).waitFor();
  meStatus = 401; await page.reload(); await page.getByRole('button', { name: 'Đăng nhập', exact: true }).waitFor();
  assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), null);
  console.log('PASS: deep-link login, F5 session restore, 503 retry preserves token, 401 clears token, unused chunks not loaded');
 } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
