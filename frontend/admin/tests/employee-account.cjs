const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = 'http://127.0.0.1:5173';
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1366, height: 900 } });
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    const user = { id: 'employee', username: 'fixture', fullName: 'Nhân viên kiểm thử', email: 'fixture@example.test', department: 'IT', role: 'Employee', status: 'Active' };
    const session = { sessionId: 'session', deviceId: 'pc1', profileId: 'profile', domainId: 'COMPANY\\fixture', deviceName: 'Máy kiểm thử' };
    const profile = { ...session, quota: { maxFiles: 10, usedFiles: 0, reservedFiles: 0, remainingFiles: 10 }, files: [] };
    let failMe = false, failChange = true, changes = 0;
    await page.route(base + '/api/**', route => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/auth/me') && failMe) return route.fulfill({ status: 500, json: { success: false, message: 'Không tải được tài khoản' } });
      if (path.endsWith('/change-password')) {
        changes++;
        assert.deepEqual(route.request().postDataJSON(), { currentPassword: 'Old@12345', newPassword: 'New@12345', confirmPassword: 'New@12345' });
        return route.fulfill({ status: failChange ? 400 : 200, json: failChange ? { success: false, message: 'Mật khẩu hiện tại không đúng.' } : { success: true, data: { changed: true } } });
      }
      const data = path.endsWith('/auth/login') ? { accessToken: 'fixture-token', user } : path.endsWith('/auth/me') ? user
        : path.endsWith('/machines') ? [{ id: 'pc1', name: session.deviceName, assetCode: 'PC1' }]
        : path.endsWith('/sessions') ? session : path.endsWith('/information') ? { ...session, software: [] } : profile;
      return route.fulfill({ json: { success: true, data } });
    });
    await page.goto(base + '/machines/pc1/login?demo=0');
    await page.getByLabel('Domain ID').fill('fixture');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('Old@12345');
    await page.getByRole('button', { name: 'Đăng nhập máy' }).click();
    await page.waitForURL('**/desktop');
    failMe = true;
    await page.getByRole('button', { name: 'Thông tin tài khoản', exact: true }).click();
    const dialog = page.getByRole('dialog', { name: 'Thông tin tài khoản' });
    await dialog.getByRole('alert').waitFor();
    failMe = false;
    await dialog.getByRole('button', { name: 'Thử lại' }).click();
    await dialog.getByText(user.email, { exact: true }).waitFor();
    await dialog.getByLabel('Mật khẩu hiện tại', { exact: true }).fill('Old@12345');
    await dialog.getByLabel('Mật khẩu mới', { exact: true }).fill('New@12345');
    await dialog.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('Different@123');
    await dialog.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
    await dialog.getByText('Mật khẩu nhập lại không khớp.', { exact: true }).waitFor();
    assert.equal(changes, 0);
    await dialog.getByLabel('Nhập lại mật khẩu mới', { exact: true }).fill('New@12345');
    await dialog.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
    await dialog.getByRole('alert').filter({ hasText: 'Mật khẩu hiện tại không đúng.' }).waitFor();
    assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), 'fixture-token');
    await page.setViewportSize({ width: 390, height: 844 });
    fs.mkdirSync('artifacts/employee-account-ui', { recursive: true });
    await page.screenshot({ path: 'artifacts/employee-account-ui/mobile.png', fullPage: true });
    failChange = false;
    await dialog.getByRole('button', { name: 'Đổi mật khẩu', exact: true }).click();
    await page.waitForURL('**/machines/pc1/login');
    await page.getByText('Đã đổi mật khẩu', { exact: true }).waitFor();
    assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), null);
    assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.deviceSession')), null);
    assert.equal(changes, 2);
    assert.deepEqual(errors, []);
    console.log('PASS account details/retry, password validation/error/success, session cleanup and mobile');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
