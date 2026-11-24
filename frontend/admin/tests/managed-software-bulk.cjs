// Isolated API fixtures: Admin bulk additions to one or more simulated machines.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.UI_BASE_URL || 'http://127.0.0.1:5173';

(async () => {
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  try {
    const page = await browser.newPage({ viewport: { width: 1440, height: 960 } });
    page.setDefaultTimeout(10000);
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const assets = [1, 2].map(n => ({ id: `pc-${n}`, assetCode: `PC-00${n}`, name: `Máy kiểm thử ${n}`, type: 'Desktop', status: 'Available' }));
    const requests = [];
    let failLoad = true, failSave = true;
    await page.addInitScript(() => sessionStorage.setItem('itdesk.accessToken', 'bulk-admin-fixture'));
    await page.route(base + '/api/**', async route => {
      const req = route.request(), path = new URL(req.url()).pathname;
      const ok = data => route.fulfill({ json: { success: true, data } });
      const fail = message => route.fulfill({ status: 503, json: { success: false, message } });
      if (path === '/api/auth/me') return ok({ id: 'admin', username: 'admin', fullName: 'Admin', role: 'Admin', status: 'Active' });
      if (path === '/api/assets/search') return ok({ items: assets, totalCount: assets.length });
      if (path === '/api/assets') {
        if (failLoad) return fail('Không tải được danh sách máy kiểm thử.');
        return ok(assets);
      }
      if (path === '/api/assets/software/bulk') {
        const body = req.postDataJSON(); requests.push(body);
        assert.equal(req.headers().authorization, 'Bearer bulk-admin-fixture');
        if (failSave) return fail('Chưa thể lưu. Dữ liệu chưa thay đổi.');
        const machines = body.assetIds.map(assetId => ({ assetId, assetName: assets.find(a => a.id === assetId).name, added: 1, skipped: body.software.length - 1 }));
        return ok({ machines, added: machines.length, skipped: machines.reduce((sum, m) => sum + m.skipped, 0) });
      }
      return ok({});
    });
    await page.goto(base + '/admin/assets?demo=0');
    await page.getByRole('button', { name: 'Thêm phần mềm hàng loạt', exact: true }).click();
    let dialog = page.getByRole('dialog');
    await dialog.getByText('Không tải được danh sách máy kiểm thử.', { exact: true }).waitFor();
    failLoad = false;
    await dialog.getByRole('button', { name: 'Tải lại danh sách máy', exact: true }).click();
    await dialog.getByLabel('Máy áp dụng', { exact: true }).waitFor();
    await dialog.getByRole('button', { name: 'Áp dụng danh sách', exact: true }).click();
    await dialog.getByText('Chọn ít nhất một máy.', { exact: true }).waitFor();
    assert.equal(requests.length, 0);
    await dialog.getByLabel('Máy áp dụng', { exact: true }).click();
    await page.getByTitle('PC-001 · Máy kiểm thử 1', { exact: true }).click();
    await dialog.getByLabel('Máy áp dụng', { exact: true }).press('Escape');
    await dialog.getByLabel('Tên phần mềm', { exact: true }).fill('  Editor  ');
    await dialog.getByLabel('Phân loại', { exact: true }).click();
    await page.getByText('Ứng dụng (App)', { exact: true }).last().click();
    await dialog.getByLabel('Phiên bản', { exact: true }).fill(' 1.0 ');
    await dialog.getByLabel('Đường dẫn (Admin)', { exact: true }).fill('C:\\Apps\\Editor.exe');
    await dialog.getByRole('button', { name: 'Thêm mục', exact: true }).click();
    await dialog.getByLabel('Tên phần mềm', { exact: true }).last().fill('Backup');
    await dialog.getByLabel('Phân loại', { exact: true }).last().click();
    await page.getByText('Dịch vụ (Service)', { exact: true }).last().click();
    await dialog.getByText('2 mục áp dụng cho 1 máy.', { exact: true }).waitFor();
    await dialog.getByRole('button', { name: 'Áp dụng danh sách', exact: true }).click();
    await dialog.getByText('Chưa thể lưu. Dữ liệu chưa thay đổi.', { exact: true }).waitFor();
    assert.equal(await dialog.getByLabel('Tên phần mềm', { exact: true }).first().inputValue(), '  Editor  ');
    assert.equal(requests.length, 1);
    assert.deepEqual(requests[0].assetIds, ['pc-1']);
    assert.equal(requests[0].software[0].name, 'Editor');
    assert.equal(requests[0].software[0].version, '1.0');
    assert.equal(requests[0].software[0].filePath, 'C:\\Apps\\Editor.exe');
    assert.equal(requests[0].software[0].kind, 'App');
    assert.equal(requests[0].software[1].kind, 'Service');
    assert.ok(requests[0].software.every(item => !('id' in item)));
    failSave = false;
    await dialog.getByRole('button', { name: 'Áp dụng danh sách', exact: true }).click();
    await dialog.getByText('Đã thêm 1 mục; bỏ qua 1 mục trùng.', { exact: true }).waitFor();
    await dialog.getByRole('button', { name: 'Đóng', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    await page.getByRole('button', { name: 'Thêm phần mềm hàng loạt', exact: true }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByLabel('Máy áp dụng', { exact: true }).click();
    await page.getByTitle('PC-001 · Máy kiểm thử 1', { exact: true }).click();
    await page.getByTitle('PC-002 · Máy kiểm thử 2', { exact: true }).click();
    await dialog.getByLabel('Máy áp dụng', { exact: true }).press('Escape');
    await dialog.getByLabel('Tên phần mềm', { exact: true }).fill('Monitoring');
    await dialog.getByLabel('Phân loại', { exact: true }).click();
    await page.getByText('Dịch vụ (Service)', { exact: true }).last().click();
    await dialog.getByText('1 mục áp dụng cho 2 máy.', { exact: true }).waitFor();
    await page.setViewportSize({ width: 390, height: 844 });
    fs.mkdirSync('artifacts/managed-software', { recursive: true });
    await page.screenshot({ path: 'artifacts/managed-software/bulk-mobile.png', fullPage: true, animations: 'disabled' });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
    await dialog.getByRole('button', { name: 'Áp dụng danh sách', exact: true }).click();
    await dialog.getByText('Đã thêm 2 mục; bỏ qua 0 mục trùng.', { exact: true }).waitFor();
    await dialog.getByRole('row').filter({ hasText: 'Máy kiểm thử 1' }).waitFor();
    await dialog.getByRole('row').filter({ hasText: 'Máy kiểm thử 2' }).waitFor();
    assert.deepEqual(requests.at(-1).assetIds, ['pc-1', 'pc-2']);
    assert.equal(requests.at(-1).software.length, 1);
    assert.deepEqual(errors, []);
    console.log('PASS bulk software: load retry, validation, one/multiple machines, App/Service, save retry/draft, counts, mobile (fixture API)');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
