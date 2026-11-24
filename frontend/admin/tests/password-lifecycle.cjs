const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.env.UI_BASE_URL || 'http://127.0.0.1:5173';
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage();
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    const user = { id: 'u1', username: 'fixture', fullName: 'Employee Test', email: 'test@example.test', department: 'IT', role: 'Employee', status: 'Active' };
    const session = { deviceId: 'pc1', sessionId: 's1', profileId: 'p1', domainId: 'COMPANY\\fixture', deviceName: 'PC1' };
    let reminder = 1, reject = true; const ack = [];
    await page.route(base + '/api/**', route => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/password-reminder')) {
        if (reject) return route.fulfill({ status: 500, json: { success: false, message: 'Thử lại thông báo' } });
        ack.push(route.request().postDataJSON().day); reminder = null;
      }
      const data = path.endsWith('/auth/login') ? { accessToken: 'fixture', user } : path.endsWith('/auth/me') ? user
        : path.endsWith('/password-policy') ? { required: true, deadline: '2026-11-26T12:00:00Z', reminderDay: reminder }
        : path.endsWith('/machines') ? [{ id: 'pc1', name: 'PC1' }] : path.endsWith('/sessions') ? session
        : path.endsWith('/information') ? { software: [] } : { ...session, quota: { maxFiles: 10, usedFiles: 0, remainingFiles: 10 }, files: [] };
      return route.fulfill({ json: { success: true, data } });
    });
    await page.goto(base + '/machines/pc1/login?demo=0');
    await page.getByLabel('Domain ID').fill('fixture');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('12345Az@');
    await page.getByRole('button', { name: 'Đăng nhập máy' }).click();
    const notice = page.getByRole('dialog', { name: 'Nhắc đổi mật khẩu' });
    await notice.getByRole('button', { name: 'Bỏ qua lần này' }).click();
    await notice.getByText('Thử lại thông báo', { exact: true }).waitFor();
    reject = false;
    await notice.getByRole('button', { name: 'Bỏ qua lần này' }).click();
    await notice.waitFor({ state: 'hidden' });
    await page.reload();
    await page.getByRole('button', { name: 'Thông tin tài khoản' }).waitFor();
    assert.deepEqual(ack, [1]);
    for (const day of [5, 6, 7]) {
      reminder = day;
      await page.evaluate(() => window.dispatchEvent(new Event('focus')));
      await notice.getByText(`Đã đến ngày ${day} của chu kỳ đổi mật khẩu.`).waitFor();
      if (day === 7) {
        await notice.getByRole('button', { name: 'Đổi ngay' }).click();
        await page.getByRole('dialog', { name: 'Thông tin tài khoản' }).getByLabel('Mật khẩu hiện tại').waitFor();
      } else {
        await notice.getByRole('button', { name: 'Bỏ qua lần này' }).click();
        await notice.waitFor({ state: 'hidden' });
      }
    }
    assert.deepEqual(ack, [1, 5, 6]); assert.deepEqual(errors, []);
    console.log('PASS first/5/6/7 reminders, skip retry, F5 persistence and change-now');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
