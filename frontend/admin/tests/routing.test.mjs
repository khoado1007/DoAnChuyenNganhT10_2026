import test from 'node:test';
import assert from 'node:assert/strict';

test('Admin audit and file metadata links survive reload and preserve role boundaries', async () => {
  const { resolveRoute, destinationAfterLogin } = await import('../src/routing.ts');
  for (const section of ['audit-log', 'files-admin']) {
    const path = `/admin/${section}`;
    assert.deepEqual(resolveRoute(path, 'Admin'), { kind: 'admin', section });
    assert.equal(destinationAfterLogin(path, 'Admin'), path);
    assert.deepEqual(resolveRoute(path, 'Employee'), { kind: 'forbidden' });
    assert.deepEqual(resolveRoute(path, 'Technician'), { kind: 'forbidden' });
    assert.deepEqual(resolveRoute(path + '/unknown', 'Admin'), { kind: 'not-found' });
  }
});

test('default and employee entries show machine selection before authentication', async () => {
  const { isMachinePath } = await import('../src/routing.ts');
  for (const path of ['/', '/employee', '/employee/', '/machines', '/machines/pc-1/login', '/machines/pc-1/desktop']) assert.equal(isMachinePath(path), true);
  for (const path of ['/admin', '/admin/assets', '/technician', '/employees', '/employee/tickets', '/technician/tickets']) assert.equal(isMachinePath(path), false);
});

test('employee ticket portal route keeps role boundaries and survives reload', async () => {
  const { resolveRoute, destinationAfterLogin } = await import('../src/routing.ts');
  for (const [path, role] of [['/employee/tickets', 'Employee'], ['/technician/tickets', 'Technician']]) {
    assert.deepEqual(resolveRoute(path, role), { kind: 'portal' });
    assert.equal(destinationAfterLogin(path, role), path);
  }
  assert.deepEqual(resolveRoute('/employee/tickets', 'Admin'), { kind: 'forbidden' });
  assert.deepEqual(resolveRoute('/employee/tickets', 'Technician'), { kind: 'forbidden' });
  assert.deepEqual(resolveRoute('/employee/other', 'Employee'), { kind: 'not-found' });
  assert.deepEqual(resolveRoute('/technician/tickets', 'Employee'), { kind: 'forbidden' });
});

test('login entries accept only their intended roles', async () => {
  const { canUseLoginPortal } = await import('../src/routing.ts');
  assert.equal(canUseLoginPortal(true, 'Admin'), true);
  assert.equal(canUseLoginPortal(true, 'Employee'), false);
  assert.equal(canUseLoginPortal(true, 'Technician'), false);
  assert.equal(canUseLoginPortal(false, 'Admin'), false);
  assert.equal(canUseLoginPortal(false, 'Employee'), true);
  assert.equal(canUseLoginPortal(false, 'Technician'), true);
});

// Kiểm tra phân quyền chính xác theo nhóm route và từ chối đường dẫn con không hợp lệ.
test('route groups enforce exact roles and reject unknown subpaths', async () => {
  const { resolveRoute, homeForRole, anonymousLandingPath } = await import('../src/routing.ts');
  assert.equal(homeForRole('Employee'), '/employee');
  assert.equal(anonymousLandingPath, '/employee');
  assert.deepEqual(resolveRoute('/admin/users/', 'Admin'), { kind: 'admin', section: 'users' });
  assert.deepEqual(resolveRoute('/technician', 'Technician'), { kind: 'portal' });
  assert.deepEqual(resolveRoute('/admin/users', 'Employee'), { kind: 'forbidden' });
  assert.deepEqual(resolveRoute('/employee', 'Admin'), { kind: 'forbidden' });
  assert.deepEqual(resolveRoute('/administrator', 'Admin'), { kind: 'not-found' });
  assert.deepEqual(resolveRoute('/admin/unknown', 'Admin'), { kind: 'not-found' });
});

// Kiểm tra sau đăng nhập chỉ khôi phục liên kết sâu phù hợp vai trò.
test('login resumes an allowed deep link and rejects other role or unknown destinations', async () => {
  const { destinationAfterLogin } = await import('../src/routing.ts');
  assert.equal(destinationAfterLogin('/employee', 'Employee'), '/employee');
  assert.equal(destinationAfterLogin('/admin', 'Admin'), '/admin');
  assert.equal(destinationAfterLogin('/admin/users', 'Admin'), '/admin/users');
  assert.equal(destinationAfterLogin('/admin/assets', 'Employee'), '/employee');
  assert.equal(destinationAfterLogin('/login', 'Admin'), '/admin');
  assert.equal(destinationAfterLogin('//evil.test', 'Admin'), '/admin');
});
