const {chromium} = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const base = process.env.UI_BASE_URL || 'http://127.0.0.1:5173';
(async () => {
  const browser = await chromium.launch({channel: 'msedge', headless: true});
  try {
    const page = await browser.newPage({viewport: {width: 1440, height: 960}, timezoneId: 'Asia/Ho_Chi_Minh'});
    const errors = []; page.on('pageerror', error => errors.push(error.message));
    const admin = {id: 'admin', username: 'admin', fullName: 'Quản trị viên', email: 'admin@test.local', role: 'Admin', status: 'Active'};
    const employee = {id: 'u1', username: 'nhanvien', fullName: 'Nhân viên A', email: 'nv@test.local', role: 'Employee', status: 'Active'};
    const technician = {id: 'tech1', username: 'kythuat', fullName: 'Kỹ thuật viên 1', email: 'kt@test.local', role: 'Technician', status: 'Active'};
    let user = admin;
    const iso = offset => new Date(Date.now() - offset).toISOString();
    const tickets = [
      {id: 't1', ticketNumber: 'TKT-2026-0101', title: 'Máy in không nhận lệnh', description: 'Máy in tầng 2 không nhận lệnh từ máy trạm.', category: 'Hardware', priority: 'High', status: 'Open', createdByName: 'Nhân viên A', assignedToUserId: null, assignedToName: null, createdAt: iso(3600000), updatedAt: null, resolvedAt: null, resolution: null, comments: [{id: 'c1', authorUsername: 'nhanvien', content: 'Máy in báo lỗi giấy kẹt dù đã kiểm tra.', isInternal: false, createdAt: iso(3000000)}], history: []},
      {id: 't2', ticketNumber: 'TKT-2026-0102', title: 'Cần cấp lại mật khẩu', description: 'Tài khoản bị khóa sau nhiều lần đăng nhập sai.', category: 'Account', priority: 'Medium', status: 'InProgress', createdByName: 'Nhân viên B', assignedToUserId: null, assignedToName: null, createdAt: iso(86400000), updatedAt: iso(3600000), resolvedAt: null, resolution: null, comments: [], history: [{fromStatus: 'Open', toStatus: 'InProgress', changedByUsername: 'admin', note: 'Đã tiếp nhận', changedAt: iso(3600000)}]},
    ];
    const requests = [];
    const detail = ticket => ({...ticket, createdByUserId: 'u1', assignedToUserId: ticket.assignedToUserId ?? null, assignedToName: ticket.assignedToName ?? null});
    await page.addInitScript(() => sessionStorage.setItem('itdesk.accessToken', 'fixture'));
    await page.route(base + '/api/**', route => {
      const request = route.request();
      const url = new URL(request.url());
      const path = url.pathname;
      const method = request.method();
      const query = Object.fromEntries(url.searchParams);
      const body = request.postDataJSON?.() ?? {};
      if (path === '/api/auth/me') return route.fulfill({json: {success: true, data: user}});
      if (path === '/api/me/assets') return route.fulfill({json: {success: true, data: []}});
      if (path.startsWith('/api/users')) return route.fulfill({json: {success: true, data: {items: [admin, employee, technician], totalCount: 3, page: 1, pageSize: 100}}});
      if (path === '/api/assets') return route.fulfill({json: {success: true, data: []}});
      if (path === '/api/tickets' && method === 'GET') {
        requests.push({path, method, query});
        const term = (query.search ?? '').toLocaleLowerCase('vi');
        const rows = tickets.filter(ticket =>
          (user.role === 'Admin' || (user.role === 'Employee' && ticket.createdByName === user.fullName) || (user.role === 'Technician' && ticket.assignedToUserId === user.id))
          && (!term || `${ticket.ticketNumber} ${ticket.title}`.toLocaleLowerCase('vi').includes(term))
          && (!query.status || ticket.status === query.status)
          && (!query.category || ticket.category === query.category)
          && (!query.priority || ticket.priority === query.priority));
        return route.fulfill({json: {success: true, data: {items: rows.map(({description, comments, history, ...row}) => row), totalCount: rows.length, page: Number(query.page ?? 1), pageSize: Number(query.pageSize ?? 20), totalPages: 1, hasNextPage: false, hasPreviousPage: false}}});
      }
      if (path === '/api/tickets' && method === 'POST') {
        requests.push({path, method, body});
        const ticket = {id: 't3', ticketNumber: 'TKT-2026-0103', title: body.title, description: body.description, category: body.category, priority: body.priority, status: 'Open', createdByName: user.fullName, createdAt: iso(0), updatedAt: null, resolvedAt: null, resolution: null, comments: [], history: []};
        tickets.push(ticket);
        return route.fulfill({json: {success: true, data: detail(ticket)}});
      }
      const match = path.match(/^\/api\/tickets\/([^/]+)(?:\/(status|comments|assign))?$/);
      if (match) {
        const ticket = tickets.find(item => item.id === match[1]);
        if (!ticket) return route.fulfill({status: 404, json: {success: false, message: 'Không tìm thấy ticket.'}});
        if (!match[2] && method === 'GET') return route.fulfill({json: {success: true, data: detail(ticket)}});
        if (match[2] === 'comments' && method === 'POST') {
          requests.push({path, method, body});
          const comment = {id: 'c' + (ticket.comments.length + 2), authorUsername: user.username, content: body.content, isInternal: Boolean(body.isInternal), createdAt: iso(0)};
          ticket.comments.push(comment);
          return route.fulfill({json: {success: true, data: comment}});
        }
        if (match[2] === 'assign' && method === 'PATCH') {
          requests.push({path, method, body});
          ticket.assignedToUserId = body.assignedToUserId || null;
          ticket.assignedToName = body.assignedToUserId ? 'Kỹ thuật viên 1' : null;
          ticket.history.push({fromStatus: ticket.status, toStatus: ticket.status, changedByUsername: user.username, note: body.note ?? 'Phân công', changedAt: iso(0)});
          return route.fulfill({json: {success: true, data: detail(ticket)}});
        }
        if (match[2] === 'status' && method === 'PATCH') {
          requests.push({path, method, body});
          ticket.history.push({fromStatus: ticket.status, toStatus: body.status, changedByUsername: user.username, note: body.note, changedAt: iso(0)});
          ticket.status = body.status;
          if (body.status === 'Resolved') { ticket.resolution = body.resolution; ticket.resolvedAt = iso(0); }
          return route.fulfill({json: {success: true, data: detail(ticket)}});
        }
      }
      return route.fulfill({status: 404, json: {success: false, message: 'Unexpected fixture endpoint'}});
    });

    await page.goto(base + '/admin/tickets?demo=0');
    await page.getByRole('button', {name: 'Máy in không nhận lệnh', exact: true}).waitFor();
    assert.equal(requests.at(-1).query.pageSize, '20');
    await page.getByRole('combobox', {name: 'Trạng thái ticket'}).click();
    await page.getByTitle('Đang xử lý', {exact: true}).click();
    const filterResponse = page.waitForResponse(response => response.url().includes('/api/tickets?') && response.url().includes('status=InProgress'));
    await page.getByRole('button', {name: 'Lọc', exact: true}).click();
    await filterResponse;
    await page.getByRole('button', {name: 'Cần cấp lại mật khẩu', exact: true}).waitFor();
    assert.equal(await page.getByRole('button', {name: 'Máy in không nhận lệnh', exact: true}).count(), 0);
    await page.getByRole('button', {name: 'Đặt lại', exact: true}).click();
    await page.getByRole('button', {name: 'Máy in không nhận lệnh', exact: true}).waitFor();

    await page.getByRole('button', {name: 'Máy in không nhận lệnh', exact: true}).click();
    const dialog = page.getByRole('dialog');
    await dialog.getByText('Máy in tầng 2 không nhận lệnh từ máy trạm.').waitFor();
    await dialog.getByLabel('Nội dung bình luận').fill('Đã kiểm tra driver máy in.');
    await dialog.getByLabel('Ghi chú nội bộ (nhân viên không thấy)').check();
    const commentResponse = page.waitForResponse(response => response.url().endsWith('/comments') && response.request().method() === 'POST');
    const detailRefresh = page.waitForResponse(response => response.url().endsWith('/api/tickets/t1') && response.request().method() === 'GET');
    await dialog.getByRole('button', {name: 'Gửi bình luận', exact: true}).click();
    await commentResponse;
    await detailRefresh;
    assert.deepEqual(requests.filter(item => item.method === 'POST' && item.path.endsWith('/comments')).at(-1).body,
      {content: 'Đã kiểm tra driver máy in.', isInternal: true});
    await dialog.getByText('Đã kiểm tra driver máy in.').waitFor();
    assert.equal(await dialog.getByText('Nội bộ', {exact: true}).count(), 1);

    const assignResponse = page.waitForResponse(response => response.url().endsWith('/assign') && response.request().method() === 'PATCH');
    await dialog.getByRole('combobox', {name: 'Chọn kỹ thuật viên'}).click();
    await page.getByTitle('Kỹ thuật viên 1 (kythuat)', {exact: true}).click();
    await dialog.getByRole('button', {name: 'Lưu phân công', exact: true}).click();
    await assignResponse;
    assert.equal(requests.filter(item => item.method === 'PATCH' && item.path.endsWith('/assign')).at(-1).body.assignedToUserId, 'tech1');
    await dialog.getByText('Kỹ thuật viên 1', {exact: true}).first().waitFor();

    const statusResponse = page.waitForResponse(response => response.url().endsWith('/status') && response.request().method() === 'PATCH');
    await dialog.getByRole('button', {name: 'Bắt đầu xử lý', exact: true}).click();
    await page.getByRole('button', {name: 'Xác nhận', exact: true}).click();
    await statusResponse;
    assert.equal(requests.filter(item => item.method === 'PATCH').at(-1).body.status, 'InProgress');
    await dialog.getByText('Đang xử lý', {exact: true}).first().waitFor();
    await page.getByRole('button', {name: 'Đóng', exact: true}).click();
    await dialog.waitFor({state: 'hidden'});
    fs.mkdirSync('artifacts/ticket-ui', {recursive: true});
    await page.screenshot({path: 'artifacts/ticket-ui/admin-tickets.png', fullPage: true});

    user = technician;
    await page.goto(base + '/technician/tickets?demo=0');
    await page.getByText('Yêu cầu hỗ trợ được phân công', {exact: true}).waitFor();
    await page.getByRole('button', {name: 'Máy in không nhận lệnh', exact: true}).waitFor();
    await page.screenshot({path: 'artifacts/ticket-ui/technician-tickets.png', fullPage: true});

    user = employee;
    await page.goto(base + '/employee/tickets?demo=0');
    await page.getByText('Yêu cầu hỗ trợ', {exact: true}).waitFor();
    await page.getByRole('button', {name: 'Máy in không nhận lệnh', exact: true}).waitFor();
    assert.equal(await page.getByRole('button', {name: 'Cần cấp lại mật khẩu', exact: true}).count(), 0);
    await page.getByRole('button', {name: 'Gửi yêu cầu', exact: true}).first().click();
    const createDialog = page.getByRole('dialog');
    await createDialog.getByLabel('Tiêu đề').fill('Không mở được thư mục dự án');
    await createDialog.getByLabel('Mô tả sự cố').fill('Mở thư mục dự án trong Documents báo lỗi truy cập.');
    const createResponse = page.waitForResponse(response => response.url().endsWith('/api/tickets') && response.request().method() === 'POST');
    await createDialog.getByRole('button', {name: 'Gửi yêu cầu', exact: true}).click();
    await createResponse;
    assert.equal(requests.filter(item => item.method === 'POST' && item.path === '/api/tickets').at(-1).body.title, 'Không mở được thư mục dự án');
    await page.getByText(/Đã gửi TKT-2026-0103/).waitFor();
    await page.getByRole('button', {name: 'Không mở được thư mục dự án', exact: true}).click();
    const employeeDialog = page.getByRole('dialog');
    await employeeDialog.getByLabel('Nội dung bình luận').fill('Mình đã thử đăng xuất và đăng nhập lại.');
    const employeeComment = page.waitForResponse(response => response.url().endsWith('/comments') && response.request().method() === 'POST');
    const employeeRefresh = page.waitForResponse(response => response.url().endsWith('/api/tickets/t3') && response.request().method() === 'GET');
    await employeeDialog.getByRole('button', {name: 'Gửi bình luận', exact: true}).click();
    await employeeComment;
    await employeeRefresh;
    assert.equal(requests.filter(item => item.method === 'POST' && item.path.endsWith('/comments')).at(-1).body.isInternal, undefined);
    await employeeDialog.getByRole('button', {name: 'Đóng', exact: true}).click();
    await page.setViewportSize({width: 390, height: 844});
    await page.goto(base + '/employee/tickets?demo=0');
    await page.getByText('Yêu cầu hỗ trợ', {exact: true}).waitFor();
    await page.locator('.ant-card').filter({hasText: 'Yêu cầu hỗ trợ'}).last()
      .screenshot({path: 'artifacts/ticket-ui/employee-tickets-mobile.png'});
    await page.screenshot({path: 'artifacts/ticket-ui/employee-portal-mobile.png', fullPage: true});
    assert.deepEqual(errors, []);
    console.log('PASS Admin ticket list/filter/detail/internal comment/status flow; Employee create/list/detail/comment; scoped data; mobile; no page errors');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
