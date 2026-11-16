// Mock API browser regression: choose machine, login, session restore, readonly info, software and files.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.UI_BASE_URL || 'http://127.0.0.1:5173';
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 900 } });
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const session = { sessionId: 'fixture-session', deviceId: 'pc-1', profileId: 'profile-1', domainId: 'COMPANY\\fixture', deviceName: 'Máy kiểm thử' };
    const user = { id: 'fixture', username: 'fixture', fullName: 'Test', role: 'Employee', status: 'Active' };
    const profile = { ...session, quota: { maxFiles: 10, usedFiles: 0, reservedFiles: 0, remainingFiles: 10, overQuota: false }, files: [] };
    const info = { assetId: 'pc-1', name: session.deviceName, assetCode: 'PC-001', status: 'simulated', memoryGb: 16, diskTotalGb: 512, diskFreeGb: 256, processor: 'Intel Core i5 · mô phỏng', diskEncryption: true, operatingSystem: 'Windows mô phỏng', software: Array.from({ length: 12 }, (_, index) => ({ name: `Software ${index + 1}`, version: '1.0', filePath: `C:\\Demo\\app${index + 1}.exe` })) };
    let logins = 0, informationCalls = 0, savedSpecification = null, denyInformation = false;
    await page.route(base + '/api/**', route => {
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/auth/login')) logins++;
      if (path.endsWith('/information') && denyInformation) return route.fulfill({ status: 403, json: { success: false, message: 'Phiên không còn quyền xem cấu hình.' } });
      if (path.endsWith('/specification') && route.request().method() === 'PUT') savedSpecification = route.request().postDataJSON();
      if (path.endsWith('/information')) { informationCalls++; assert.equal(route.request().headers()['x-device-session'], session.sessionId); }
      const data = path.endsWith('/auth/login') ? { accessToken: 'fixture-token', user } : path.endsWith('/auth/me') ? user
        : path === '/api/machines' ? [{ id: 'pc-1', assetCode: 'PC-001', name: session.deviceName, mode: 'simulated' }]
        : path.endsWith('/profiles') ? [{ profileId: 'server-profile', username: 'server-user', deviceName: 'Máy server', storageBackend: 'Server', maxFiles: 10, usedFiles: 1 }, { profileId: 'agent-profile', username: 'agent-user', deviceName: 'Máy cũ', storageBackend: 'Agent', maxFiles: 10, usedFiles: 1, pendingState: 'Unknown' }]
        : path.endsWith('/assets/search') ? { items: [{ id: 'pc-1', assetCode: 'PC-001', name: session.deviceName, type: 'Desktop', status: 'Available' }], totalCount: 1 }
        : path.endsWith('/specification') || path.endsWith('/information') ? info : path.endsWith('/sessions') ? session : path.endsWith('/profile') || path.endsWith('/operations') ? profile : {};
      return route.fulfill({ json: { success: true, data } });
    });
    await page.goto(base + '/employee');
    await page.getByRole('button', { name: 'Chọn máy →' }).click();
    assert.equal(logins, 0);
    await page.getByLabel('Domain ID').fill('fixture');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('fixture-only');
    await page.getByRole('button', { name: 'Đăng nhập máy' }).click();
    await page.waitForURL('**/machines/pc-1/desktop');
    await page.getByText('Windows mô phỏng', { exact: true }).first().waitFor();
    await page.getByText('Máy đang sử dụng', { exact: true }).waitFor();
    await page.getByText('256 / 512 GB', { exact: true }).waitFor();
    fs.mkdirSync('artifacts/machine-workspace', { recursive: true });
    await page.screenshot({ path: 'artifacts/machine-workspace/overview-desktop.png', fullPage: true, animations: 'disabled' });
    denyInformation = true;
    await page.getByRole('button', { name: 'Làm mới thông tin', exact: true }).click();
    await page.getByRole('alert').filter({ hasText: 'Phiên không còn quyền' }).waitFor();
    assert.equal(await page.getByText('Windows mô phỏng', { exact: true }).count(), 0);
    denyInformation = false;
    await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
    await page.getByText('256 / 512 GB', { exact: true }).waitFor();
    assert.ok(await page.getByText('Chưa khai báo', { exact: true }).count() > 0);
    await page.getByRole('tab', { name: 'Phần mềm', exact: true }).click();
    await page.getByText('Software 1', { exact: true }).waitFor();
    assert.equal(await page.getByText('Software 11', { exact: true }).count(), 0);
    await page.getByLabel('Tìm phần mềm').fill('Software 12');
    await page.getByText('Software 12', { exact: true }).waitFor();
    assert.equal(await page.locator('a[href^="C:"]').count(), 0);
    await page.getByRole('tab', { name: 'Desktop / Documents', exact: true }).click();
    await page.getByRole('button', { name: 'Tạo TXT/CSV' }).waitFor();
    await page.reload();
    await page.getByText('Windows mô phỏng', { exact: true }).first().waitFor();
    assert.equal(logins, 1); assert.ok(informationCalls >= 2);
    await page.setViewportSize({ width: 390, height: 844 });
    await page.screenshot({ path: 'artifacts/machine-workspace/overview-mobile.png', fullPage: true, animations: 'disabled' });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await page.getByRole('button', { name: 'Đăng xuất / Đổi máy' }).click();
    await page.locator('.machine-chooser').waitFor();
    assert.equal(await page.evaluate(() => sessionStorage.getItem('itdesk.accessToken')), null);
    user.role = 'Admin';
    await page.setViewportSize({ width: 1440, height: 900 });
    await page.goto(base + '/admin/assets');
    await page.getByLabel('Tên đăng nhập', { exact: true }).fill('fixture');
    await page.getByLabel('Mật khẩu', { exact: true }).fill('fixture-only');
    await page.locator('button[type=submit]').click();
    await page.getByRole('button', { name: 'Cấu hình mô phỏng', exact: true }).click();
    await page.getByLabel('Hệ điều hành', { exact: true }).fill('Updated simulation OS');
    await page.getByLabel('Tổng ổ đĩa (GB)', { exact: true }).fill('512');
    await page.getByLabel('Ổ đĩa còn trống (GB)', { exact: true }).fill('256');
    await page.getByLabel('Mã hóa ổ đĩa', { exact: true }).click();
    await page.getByText('Có', { exact: true }).click();
    await page.getByRole('button', { name: 'Lưu cấu hình', exact: true }).click();
    await page.getByRole('dialog').waitFor({ state: 'hidden' });
    assert.equal(savedSpecification.operatingSystem, 'Updated simulation OS');
    assert.equal(savedSpecification.diskFreeGb, 256);
    assert.equal(savedSpecification.diskEncryption, true);
    assert.equal(savedSpecification.software.length, 12);
    assert.equal(await page.getByRole('navigation').getByRole('button', { name: 'Agent', exact: true }).count(), 0);
    await page.goto(base + '/admin/quotas');
    const serverRow = page.getByRole('row').filter({ hasText: 'server-user' });
    const agentRow = page.getByRole('row').filter({ hasText: 'agent-user' });
    await serverRow.getByText('Server mô phỏng', { exact: true }).waitFor();
    assert.equal(await serverRow.getByRole('button', { name: 'Đối soát', exact: true }).count(), 0);
    await agentRow.getByRole('button', { name: 'Đối soát', exact: true }).waitFor();
    assert.deepEqual(errors, []);
    console.log('PASS machine selection, login, readonly details, software search/paging, files, F5, mobile, logout, admin specification save (mock API)');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
