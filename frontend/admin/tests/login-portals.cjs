// UI smoke với API giả lập; không dùng tài khoản hoặc database nghiệp vụ.
// PLAYWRIGHT_MODULE: đường dẫn module Playwright nếu chưa cài tại workspace.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.UI_BASE_URL || 'http://127.0.0.1:5173';
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  let checks = 0;
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    let role = 'Admin';
    await page.route(base + '/api/**', route => {
      const path = new URL(route.request().url()).pathname;
      const user = { id: 'fixture-user', username: 'fixture', fullName: 'UI Test', role, status: 'Active', email: 'test@example.test', department: 'IT' };
      const data = path.endsWith('/auth/login') ? { accessToken: 'ui-fixture-token', user }
        : path.endsWith('/auth/me') ? user
        : ['/api/me/assets', '/api/machines'].includes(path) ? [] : { items: [], totalCount: 0, page: 1, pageSize: 20 };
      return route.fulfill({ json: { success: true, data } });
    });
    const submit = async () => {
      await page.getByLabel('Tên đăng nhập', { exact: true }).fill('fixture');
      await page.getByLabel('Mật khẩu', { exact: true }).fill('fixture-only');
      await page.locator('button[type=submit]').click();
    };
    const noAdminLink = async () => assert.equal(await page.locator('a[href^="/admin"]').count(), 0);
    await page.goto(base);
    await page.waitForURL('**/employee');
    await page.locator('.machine-chooser').waitFor();
    assert.equal(await page.locator('input[type=password]').count(), 0);
    await noAdminLink(); checks++;
    role = 'Employee';
    await page.goto(base + '/admin');
    await page.locator('.admin-login-card').waitFor();
    assert.equal(await page.locator('.admin-login-page').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(27, 42, 74)');
    assert.equal(await page.locator('.admin-login-card').evaluate(el => getComputedStyle(el).backgroundColor), 'rgb(255, 255, 255)'); checks++;
    await submit();
    await page.getByRole('alert').filter({ hasText: 'Tài khoản không có quyền' }).waitFor();
    assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), null); checks++;
    await page.reload();
    fs.mkdirSync('artifacts/login-portals', { recursive: true });
    await page.locator('.admin-login-card').waitFor();
    await page.screenshot({ path: 'artifacts/login-portals/admin.png', fullPage: true, animations: 'disabled' });
    for (const path of ['/admin', '/employee']) {
      await page.setViewportSize({ width: 390, height: 844 });
      await page.goto(base + path);
      await page.locator(path === '/admin' ? 'button[type=submit]' : '.machine-chooser').waitFor();
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
      await page.screenshot({ path: `artifacts/login-portals/${path.slice(1)}-mobile.png`, fullPage: true, animations: 'disabled' });
      checks++;
    }
    await page.goto(base + '/machines');
    await page.getByRole('heading', { name: 'Không gian máy công ty' }).waitFor();
    await noAdminLink(); checks++;
    role = 'Admin';
    await page.goto(base + '/admin/users');
    await submit();
    await page.waitForFunction(() => !document.querySelector('.admin-login-card'));
    assert.equal(new URL(page.url()).pathname, '/admin/users');
    assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), 'ui-fixture-token'); checks++;
    console.log(`PASS ${checks} login portal UI checks (mock API)`);
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
