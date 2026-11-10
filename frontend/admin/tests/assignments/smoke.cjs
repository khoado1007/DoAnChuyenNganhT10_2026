const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const base = process.env.ITAM_UI_URL || 'http://127.0.0.1:5190';
// Kiểm thử cấp phát, hiển thị cho nhân viên, trả, cấp lại và thu hồi tài sản.
(async () => {
 const browser = await chromium.launch({ channel: 'msedge', headless: true });
 try {
  const admin = await browser.newPage();
  // Đăng nhập qua giao diện API và lưu token cho phiên trình duyệt kiểm thử.
  const login = async (page, username, password) => {
   const result = await page.request.post(base + '/api/auth/login', { data: { username, password } });
   assert.equal(result.status(), 200); const token = (await result.json()).data.accessToken;
   await page.addInitScript(jwt => sessionStorage.setItem('itdesk.accessToken', jwt), token); return token;
  };
  const jwt = await login(admin, 'admin', process.env.ITAM_TEST_PASSWORD);
  const headers = { Authorization: 'Bearer ' + jwt };
  const name = 'allocation' + Date.now();
  const created = await admin.request.post(base + '/api/users', { headers, data: { username: name, email: name + '@test.local', fullName: name, department: 'IT', role: 'Employee', password: 'Test@12345' } });
  assert.equal(created.status(), 200);
  const asset = await admin.request.post(base + '/api/assets', { headers, data: { assetCode: name, name: 'Test allocation laptop' } });
  assert.equal(asset.status(), 200);
  const owner = await browser.newPage({ viewport: { width: 390, height: 844 } });
  await login(owner, name, 'Test@12345');
  await admin.goto(base + '/admin/assets');
  await admin.getByPlaceholder('Mã, tên, serial').fill(name); await admin.getByPlaceholder('Mã, tên, serial').press('Enter');
  const row = admin.getByRole('row').filter({ hasText: name });
  await row.getByRole('button', { name: 'Cấp phát / lịch sử', exact: true }).click();
  const dialog = admin.getByRole('dialog');
  await dialog.getByPlaceholder('Tìm nhân viên theo tên đăng nhập').fill(name);
  await dialog.getByPlaceholder('Tìm nhân viên theo tên đăng nhập').press('Enter');
  await dialog.getByLabel('Người nhận', { exact: true }).press('ArrowDown');
  await admin.getByTitle(name, { exact: true }).click();
  await dialog.getByRole('button', { name: 'Xác nhận cấp phát', exact: true }).click();
  await dialog.getByText('Đang sử dụng', { exact: true }).waitFor();
  await owner.goto(base + '/employee'); await owner.getByRole('cell', { name, exact: true }).waitFor();
  await owner.reload(); await owner.getByRole('cell', { name, exact: true }).waitFor();
  assert.equal(await owner.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true);
  await dialog.getByRole('button', { name: 'Close', exact: true }).click();
  await dialog.waitFor({ state: 'hidden' });
  await admin.getByRole('combobox', { name: 'Lọc vòng đời' }).press('ArrowDown');
  await admin.getByTitle('Đã cấp phát', { exact: true }).click();
  await row.getByRole('button', { name: 'Cấp phát / lịch sử', exact: true }).click();
  await dialog.getByText('Đang sử dụng', { exact: true }).waitFor();
  await dialog.getByRole('button', { name: 'Xác nhận trả', exact: true }).click();
  await dialog.getByText('Đã trả', { exact: true }).waitFor();
  await owner.reload(); await owner.getByText('Bạn chưa được cấp tài sản.').waitFor();
  await dialog.getByPlaceholder('Tìm nhân viên theo tên đăng nhập').fill(name);
  await dialog.getByPlaceholder('Tìm nhân viên theo tên đăng nhập').press('Enter');
  await dialog.getByLabel('Người nhận', { exact: true }).press('ArrowDown');
  await admin.getByTitle(name, { exact: true }).click();
  await dialog.getByRole('button', { name: 'Xác nhận cấp phát', exact: true }).click();
  await dialog.getByText('Đang sử dụng', { exact: true }).waitFor();
  await dialog.getByRole('button', { name: 'Thu hồi', exact: true }).click();
  await dialog.getByText('Đã thu hồi', { exact: true }).waitFor();
  await owner.reload(); await owner.getByText('Bạn chưa được cấp tài sản.').waitFor();
  console.log('PASS: assign/employee visibility/reload/mobile/return/reassign/revoke/history');
 } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });



