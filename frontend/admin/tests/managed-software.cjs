// Browser acceptance for Admin-confirmed software removal; isolated API fixtures, no business data.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.UI_BASE_URL || 'http://127.0.0.1:5173';
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    page.setDefaultTimeout(45000);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const session = { sessionId: 'fixture-session', deviceId: 'pc-1', profileId: 'profile-1', domainId: 'COMPANY\\fixture', deviceName: 'Máy kiểm thử' };
    const user = { id: 'fixture', username: 'fixture', fullName: 'Test', role: 'Employee', status: 'Active' };
    const profile = { ...session, quota: { maxFiles: 10, usedFiles: 0, reservedFiles: 0, remainingFiles: 10, overQuota: false }, files: [] };
    const info = { assetId: 'pc-1', name: session.deviceName, assetCode: 'PC-001', status: 'simulated', revision: 'r1', software: [
      { id: 'app-1', name: 'Editor', kind: 'App', type: 'Utility', version: '1.0', filePath: null },
      { id: 'svc-1', name: 'Backup Service', kind: 'Service', type: null, version: '2.0', filePath: null },
    ] };
    let logins = 0, removalCalls = 0, denyMode = 'credentials', saved = null;
    await page.route(base + '/api/**', route => {
      const request = route.request(), path = new URL(request.url()).pathname;
      const ok = data => route.fulfill({ json: { success: true, data } });
      const fail = (status, message) => route.fulfill({ status, json: { success: false, message } });
      if (path.endsWith('/auth/login')) { logins++; return ok({ accessToken: 'employee-fixture-token', user }); }
      if (path.endsWith('/auth/me')) return ok(user);
      if (path.endsWith('/remove')) {
        removalCalls++;
        const body = request.postDataJSON();
        assert.equal(request.headers().authorization, 'Bearer employee-fixture-token');
        assert.equal(request.headers()['x-device-session'], session.sessionId);
        assert.equal(body.adminUsername, 'admin-fixture');
        assert.equal(body.revision, info.revision);
        if (denyMode === 'credentials') return fail(403, 'Tài khoản Admin không hợp lệ.');
        if (denyMode === 'limit') return fail(429, 'Quá nhiều yêu cầu; vui lòng thử lại sau.');
        if (denyMode === 'changed') return fail(409, 'Cấu hình đã thay đổi. Vui lòng tải lại.');
        assert.equal(body.adminPassword, 'admin-fixture-password');
        const itemId = path.split('/').at(-2);
        info.software = info.software.filter(item => item.id !== itemId);
        info.revision = 'r2';
        return ok(info);
      }
      if (path.endsWith('/specification') && request.method() === 'PUT') {
        saved = request.postDataJSON();
        return fail(409, 'Cấu hình đã thay đổi. Vui lòng tải lại.');
      }
      return ok(path === '/api/machines' ? [{ id: 'pc-1', assetCode: 'PC-001', name: session.deviceName, mode: 'simulated' }]
        : path.endsWith('/information') || path.endsWith('/specification') ? info
        : path.endsWith('/assets/search') ? { items: [{ id: 'pc-1', assetCode: 'PC-001', name: session.deviceName, type: 'Desktop', status: 'Available' }], totalCount: 1 }
        : path.endsWith('/sessions') ? session : path.endsWith('/profile') || path.endsWith('/operations') ? profile : {});
    });
    await page.goto(base + '/employee?demo=0');
    await page.getByRole('button', { name: 'Chọn máy →' }).click();
    await page.getByLabel('Domain ID').fill('fixture');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('employee-fixture-password');
    await page.getByRole('button', { name: 'Đăng nhập máy' }).click();
    await page.waitForURL('**/machines/pc-1/desktop');
    await page.getByRole('tab', { name: 'Phần mềm', exact: true }).click();
    const row = page.getByRole('row').filter({ hasText: 'Editor' });
    await row.getByRole('button', { name: 'Yêu cầu xóa', exact: true }).click();
    let dialog = page.getByRole('dialog');
    await dialog.getByText('Editor', { exact: true }).waitFor();
    await dialog.getByLabel('Tài khoản Admin', { exact: true }).fill('admin-fixture');
    await dialog.getByLabel('Mật khẩu Admin', { exact: true }).fill('wrong-fixture-password');
    await dialog.getByRole('button', { name: 'Xác nhận và xóa', exact: true }).click();
    await dialog.getByText('Tài khoản Admin không hợp lệ.', { exact: true }).waitFor();
    assert.equal(await dialog.getByLabel('Mật khẩu Admin', { exact: true }).inputValue(), '');
    assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), 'employee-fixture-token');
    await row.getByText('Editor', { exact: true }).waitFor();
    denyMode = 'limit';
    await dialog.getByLabel('Mật khẩu Admin', { exact: true }).fill('retry-fixture-password');
    await dialog.getByRole('button', { name: 'Xác nhận và xóa', exact: true }).click();
    await dialog.getByText('Quá nhiều yêu cầu; vui lòng thử lại sau.', { exact: true }).waitFor();
    denyMode = '';
    await dialog.getByLabel('Mật khẩu Admin', { exact: true }).fill('admin-fixture-password');
    await dialog.getByRole('button', { name: 'Xác nhận và xóa', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.equal(await page.getByRole('row').filter({ hasText: 'Editor' }).count(), 0);
    await page.getByRole('row').filter({ hasText: 'Backup Service' }).waitFor();
    assert.equal(logins, 1); assert.equal(removalCalls, 3);
    await page.reload();
    await page.getByRole('tab', { name: 'Phần mềm', exact: true }).click();
    await page.getByRole('row').filter({ hasText: 'Backup Service' }).getByRole('button', { name: 'Yêu cầu xóa', exact: true }).click();
    dialog = page.getByRole('dialog');
    assert.equal(await dialog.getByLabel('Mật khẩu Admin', { exact: true }).inputValue(), '');
    denyMode = 'changed';
    await dialog.getByLabel('Tài khoản Admin', { exact: true }).fill('admin-fixture');
    await dialog.getByLabel('Mật khẩu Admin', { exact: true }).fill('admin-fixture-password');
    await dialog.getByRole('button', { name: 'Xác nhận và xóa', exact: true }).click();
    await dialog.getByText('Cấu hình đã thay đổi. Vui lòng tải lại.', { exact: true }).waitFor();
    await page.setViewportSize({ width: 390, height: 844 });
    fs.mkdirSync('artifacts/managed-software', { recursive: true });
    await page.screenshot({ path: 'artifacts/managed-software/admin-confirm-mobile.png', fullPage: true, animations: 'disabled' });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await dialog.getByRole('button', { name: 'Hủy', exact: true }).click();
    assert.equal(await page.evaluate(() => JSON.stringify({ ...sessionStorage, ...localStorage }).includes('admin-fixture-password')), false);
    await page.getByRole('tab', { name: 'Desktop / Documents', exact: true }).click();
    await page.getByRole('button', { name: 'Tạo TXT/CSV' }).waitFor();
    // Existing Admin form preserves kind/ID/revision; conflict is visible without losing the draft.
    user.role = 'Admin';
    await page.setViewportSize({ width: 1440, height: 960 });
    await page.goto(base + '/admin/assets');
    await page.getByRole('button', { name: 'Cấu hình mô phỏng', exact: true }).click();
    await page.getByRole('button', { name: 'Thêm phần mềm', exact: true }).click();
    await page.getByLabel('Tên phần mềm', { exact: true }).last().fill('New App');
    await page.getByLabel('Phân loại', { exact: true }).last().press('ArrowDown');
    await page.locator('.ant-select-dropdown:visible .ant-select-item-option-content').filter({ hasText: 'Ứng dụng (App)' }).click();
    await page.getByRole('button', { name: 'Lưu cấu hình', exact: true }).click();
    await page.getByRole('alert').filter({ hasText: 'Cấu hình đã thay đổi.' }).waitFor();
    assert.equal(saved.revision, 'r2');
    assert.equal(saved.software[0].id, 'svc-1');
    assert.equal(saved.software[0].kind, 'Service');
    assert.equal(saved.software[1].kind, 'App');
    assert.equal(await page.getByLabel('Tên phần mềm', { exact: true }).last().inputValue(), 'New App');
    assert.deepEqual(errors, []);
    console.log('PASS managed software: approval errors/retry/removal/F5/session isolation/conflict/mobile/Admin kind+revision (fixture API)');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
