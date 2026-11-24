const {chromium, request} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.UI_BASE_URL || 'http://127.0.0.1:5173';
const tag = Date.now().toString(36).slice(-6);
const username = `ticket${tag}`;
const password = 'Test@12345';
(async () => {
  const api = await request.newContext({baseURL: base});
  const login = await api.post('/api/auth/login', {data: {username: 'admin', password: 'Admin@12345'}});
  assert.equal(login.status(), 200, await login.text());
  const adminAuth = await login.json();
  const created = await api.post('/api/users', {
    headers: {Authorization: `Bearer ${adminAuth.data.accessToken}`},
    data: {username, email: `${username}@test.local`, fullName: 'Nhân viên Ticket E2E', department: 'E2E',
      password, role: 'Employee'},
  });
  assert.equal(created.status(), 200, await created.text());

  const browser = await chromium.launch({channel: 'msedge', headless: true});
  try {
    const page = await browser.newPage({viewport: {width: 1440, height: 960}, timezoneId: 'Asia/Ho_Chi_Minh'});
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const title = `Không mở được thư mục dự án ${tag}`;

    await page.goto(base + '/employee/tickets?demo=0');
    await page.getByLabel('Tên đăng nhập').fill(username);
    await page.getByLabel('Mật khẩu').fill(password);
    await page.getByRole('button', {name: /Đăng nhập/}).click();
    await page.getByText('Yêu cầu hỗ trợ', {exact: true}).waitFor();
    await page.getByRole('button', {name: 'Gửi yêu cầu', exact: true}).first().click();
    const createDialog = page.getByRole('dialog');
    await createDialog.getByLabel('Tiêu đề').fill(title);
    await createDialog.getByLabel('Mô tả sự cố').fill('Mở thư mục dự án trong Documents báo lỗi truy cập và không thể lưu tệp.');
    await createDialog.getByRole('button', {name: 'Gửi yêu cầu', exact: true}).click();
    await page.getByText(new RegExp('Đã gửi TKT-')).waitFor();
    await page.getByRole('button', {name: title, exact: true}).click();
    const employeeDialog = page.getByRole('dialog');
    await employeeDialog.getByLabel('Nội dung bình luận').fill('Mình đã thử đăng xuất và đăng nhập lại nhưng vẫn lỗi.');
    await employeeDialog.getByRole('button', {name: 'Gửi bình luận', exact: true}).click();
    await employeeDialog.getByText('Mình đã thử đăng xuất và đăng nhập lại nhưng vẫn lỗi.').waitFor();
    await employeeDialog.getByRole('button', {name: 'Đóng', exact: true}).click();
    console.log('PASS employee: login, create ticket, comment');

    await page.evaluate(() => sessionStorage.clear());
    await page.goto(base + '/admin/tickets?demo=0');
    await page.getByLabel('Tên đăng nhập').fill('admin');
    await page.getByLabel('Mật khẩu').fill('Admin@12345');
    await page.getByRole('button', {name: /Đăng nhập/}).click();
    await page.getByRole('heading', {name: 'Yêu cầu hỗ trợ'}).waitFor();
    await page.getByLabel('Tìm ticket').fill(tag);
    await page.getByRole('button', {name: 'Lọc', exact: true}).click();
    await page.getByRole('button', {name: title, exact: true}).waitFor();
    await page.getByRole('button', {name: title, exact: true}).click();
    const adminDialog = page.getByRole('dialog');
    await adminDialog.getByText('Mình đã thử đăng xuất và đăng nhập lại nhưng vẫn lỗi.').waitFor();
    assert.equal(await adminDialog.getByText('Nội bộ', {exact: true}).count(), 0);
    await adminDialog.getByLabel('Nội dung bình luận').fill('Đã kiểm tra quyền thư mục trên máy chủ.');
    await adminDialog.getByLabel('Ghi chú nội bộ (nhân viên không thấy)').check();
    await adminDialog.getByRole('button', {name: 'Gửi bình luận', exact: true}).click();
    await adminDialog.getByText('Đã kiểm tra quyền thư mục trên máy chủ.').waitFor();
    await adminDialog.getByText('Nội bộ', {exact: true}).waitFor();
    await adminDialog.getByRole('button', {name: 'Bắt đầu xử lý', exact: true}).click();
    await page.getByRole('button', {name: 'Xác nhận', exact: true}).click();
    await adminDialog.getByText('Đang xử lý', {exact: true}).first().waitFor();
    await adminDialog.getByLabel('Kết quả xử lý').fill('Đã cấp lại quyền thư mục và kiểm tra lưu tệp thành công.');
    await adminDialog.getByRole('button', {name: 'Hoàn tất xử lý', exact: true}).click();
    await page.getByRole('button', {name: 'Xác nhận', exact: true}).click();
    await adminDialog.getByText('Đã xử lý', {exact: true}).first().waitFor();
    await adminDialog.getByRole('button', {name: 'Đóng', exact: true}).click();
    await page.getByRole('button', {name: 'Đóng', exact: true}).waitFor({state: 'hidden'});
    await page.reload();
    await page.getByRole('heading', {name: 'Yêu cầu hỗ trợ'}).waitFor();
    await page.getByLabel('Tìm ticket').fill(tag);
    await page.getByRole('button', {name: 'Lọc', exact: true}).click();
    await page.getByRole('button', {name: title, exact: true}).click();
    await page.getByRole('dialog').getByText('Đã cấp lại quyền thư mục và kiểm tra lưu tệp thành công.').waitFor();
    assert.equal(await page.getByRole('dialog').getByText('Nội bộ', {exact: true}).count(), 1);
    fs.mkdirSync('artifacts/ticket-ui', {recursive: true});
    await page.screenshot({path: 'artifacts/ticket-ui/admin-live.png', fullPage: true});
    assert.deepEqual(errors, []);
    console.log('PASS admin: login, filter, internal note, status flow, reload persistence');
  } finally {
    await browser.close();
    await api.dispose();
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
