const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.UI_BASE_URL || 'http://127.0.0.1:5173';
(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 960 }, timezoneId: 'Asia/Ho_Chi_Minh' });
    const errors = []; page.on('pageerror', e => errors.push(e.message));
    const user = { id: 'admin', username: 'admin', fullName: 'Admin Test', email: 'admin@test.local', role: 'Admin', status: 'Active' };
    const file = { id: 'f1', profileId: 'p1', userId: 'u101', userName: 'Nhân viên cuối', assetId: 'a1', assetName: 'PC kiểm thử', folder: 'Documents', name: 'bao-cao.txt', extension: '.txt', sizeBytes: 1024, storageBackend: 'Server' };
    const event = { id: 'e1', actorId: 'u101', actorName: 'Nhân viên cuối', action: 'ServerFileCreate', resourceId: 'op1', createdAt: '2026-11-23T01:30:00Z' };
    let failAssets = true, failRows = false;
    const calls = [], userPages = [];
    await page.addInitScript(() => sessionStorage.setItem('itdesk.accessToken', 'fixture'));
    await page.route(base + '/api/**', route => {
      const url = new URL(route.request().url()), path = url.pathname, q = url.searchParams;
      if (path === '/api/auth/me') return route.fulfill({ json: { success: true, data: user } });
      if (path === '/api/users') {
        assert.ok(Number(q.get('pageSize')) <= 100, 'User API rejects pageSize >100');
        userPages.push(q.get('page'));
        return route.fulfill({ json: { success: true, data: { items: q.get('page') === '2' ? [{ id: 'u101', fullName: 'Nhân viên cuối' }] : Array.from({ length: 100 }, (_, i) => ({ id: `u${i}`, fullName: `Nhân viên ${i}` })), totalCount: 101 } } });
      }
      if (path === '/api/assets') return route.fulfill(failAssets ? { status: 500, json: { success: false, message: 'Danh sách máy tạm lỗi' } } : { json: { success: true, data: [{ id: 'a1', name: 'PC kiểm thử' }] } });
      if (path === '/api/admin/files' || path === '/api/admin/audit-log') {
        calls.push({ path, query: Object.fromEntries(q) });
        if (failRows) return route.fulfill({ status: 500, json: { success: false, message: 'Dữ liệu tạm lỗi' } });
        const empty = q.get('folder') === 'RecycleBin';
        return route.fulfill({ json: { success: true, data: { items: empty ? [] : [path.endsWith('/files') ? file : event], totalCount: empty ? 0 : 51, page: Number(q.get('page')), pageSize: 50 } } });
      }
      return route.fulfill({ status: 404, json: { success: false, message: 'Unexpected fixture endpoint' } });
    });
    await page.goto(base + '/admin/files-admin?demo=0');
    await page.getByText('bao-cao.txt', { exact: true }).waitFor();
    await page.getByRole('alert').filter({ hasText: 'Không tải được danh sách bộ lọc' }).waitFor();
    failAssets = false;
    await page.getByRole('button', { name: 'Tải lại bộ lọc' }).click();
    await page.getByRole('combobox', { name: 'Lọc theo máy' }).click();
    await page.getByTitle('PC kiểm thử', { exact: true }).last().click();
    const pageTwo = page.waitForResponse(r => r.url().includes('/admin/files?') && r.url().includes('page=2'));
    await page.getByRole('listitem', { name: '2', exact: true }).click();
    await pageTwo;
    assert.equal(calls.at(-1).query.assetId, undefined, 'Draft filter must not apply during pagination');
    const filtered = page.waitForResponse(r => r.url().includes('/admin/files?') && r.url().includes('assetId=a1'));
    await page.getByRole('button', { name: 'Lọc', exact: true }).click(); await filtered;
    assert.equal(calls.at(-1).query.page, '1');
    await page.getByRole('combobox', { name: 'Lọc theo tài khoản' }).fill('Nhân viên cuối');
    await page.getByTitle('Nhân viên cuối', { exact: true }).last().click();
    const byUser = page.waitForResponse(r => r.url().includes('userId=u101'));
    await page.getByRole('button', { name: 'Lọc', exact: true }).click(); await byUser;
    assert.ok(userPages.includes('2'));
    await page.getByRole('combobox', { name: 'Thư mục', exact: true }).click();
    await page.getByTitle('Thùng rác', { exact: true }).click();
    await page.getByRole('button', { name: 'Lọc', exact: true }).click();
    await page.getByText('Chưa có file nào.').waitFor();
    failRows = true;
    await page.getByRole('button', { name: 'Đặt lại' }).click();
    await page.getByRole('alert').filter({ hasText: 'Dữ liệu tạm lỗi' }).waitFor();
    assert.equal(await page.getByText('bao-cao.txt', { exact: true }).count(), 0);
    failRows = false; await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
    await page.getByText('bao-cao.txt', { exact: true }).waitFor();
    await page.reload(); await page.getByText('bao-cao.txt', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Nhật ký thao tác', exact: true }).click();
    await page.waitForURL('**/admin/audit-log');
    await page.getByText('Tạo file', { exact: true }).waitFor();
    await page.getByText(/08:30:00/).waitFor();
    await page.getByRole('combobox', { name: 'Loại thao tác' }).click();
    assert.equal(await page.getByTitle('Đăng nhập máy', { exact: true }).count(), 0);
    await page.getByTitle('Ghi file', { exact: true }).click();
    const action = page.waitForResponse(r => r.url().includes('action=ServerFileWrite'));
    await page.getByRole('button', { name: 'Lọc', exact: true }).click(); await action;
    await page.reload(); await page.getByText('Tạo file', { exact: true }).waitFor();
    fs.mkdirSync('artifacts/admin-audit-ui', { recursive: true });
    await page.screenshot({ path: 'artifacts/admin-audit-ui/audit-desktop.png', fullPage: true });
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(base + '/admin/files-admin'); await page.getByText('bao-cao.txt', { exact: true }).waitFor();
    await page.screenshot({ path: 'artifacts/admin-audit-ui/files-mobile.png', fullPage: true });
    assert.deepEqual(errors, []);
    console.log('PASS Admin deep links/F5/navigation; array assets and all user pages; filters/paging/reset; errors/retry/empty; UTC display; desktop/mobile; no file content actions');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
