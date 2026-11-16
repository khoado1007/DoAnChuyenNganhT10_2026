const { rememberSecret, redactError } = require('./support.cjs');
// Real HTTP + interrupted Python worker. This proves conservative failure handling,
// not automatic recovery: an unknown operation currently needs reconciliation.
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
assert.match(process.env.ITAM_TEST_RUN || '', /^[0-9a-f]{32}$/, 'Use the isolated Python runner');
const server = process.env.ITAM_API_URL;
assert.equal(new URL(server).hostname, '127.0.0.1');
assert.equal(new URL(server).protocol, 'http:');

async function request(route, { method = 'GET', token, session, body } = {}) {
  const response = await fetch(server + '/api' + route, {
    method, headers: { 'Content-Type': 'application/json',
      ...(token ? { Authorization: 'Bearer ' + token } : {}),
      ...(session ? { 'X-Device-Session': session } : {}) },
    body: body === undefined ? undefined : JSON.stringify(body),
    signal: AbortSignal.timeout(30000),
  });
  return { status: response.status, body: await response.json() };
}

(async () => {
  const login = await request('/auth/login', { method: 'POST', body: {
    username: 'live_employee', password: process.env.ITAM_TEST_PASSWORD,
  } });
  assert.equal(login.status, 200);
  const token = rememberSecret(login.body.data.accessToken);
  const route = '/machines/' + process.env.ITAM_DEVICE;
  const started = await request(route + '/sessions', { method: 'POST', token, body: {} });
  assert.equal(started.status, 200);
  const session = started.body.data.sessionId;
  const op = body => request(route + '/files/operations', {
    method: 'POST', token, session, body: { requestId: crypto.randomUUID(), ...body },
  });
  assert.equal((await op({ kind: 'ensure', folder: 'Desktop' })).status, 200);
  assert.equal((await op({ kind: 'create', folder: 'Desktop', name: 'kept.txt', contentBase64: Buffer.from('keep this document').toString('base64') })).status, 200);
  const quota = await request('/profiles/' + started.body.data.profileId + '/quota', {
    method: 'PUT', token: process.env.ITAM_ADMIN_TOKEN, body: { maxFiles: 2 },
  });
  assert.equal(quota.status, 200);
  const mutation = { requestId: crypto.randomUUID(), kind: 'create', folder: 'Desktop',
    name: 'fault-before.txt', contentBase64: Buffer.from('must not be written').toString('base64') };
  const interrupted = await op(mutation);
  assert.equal(interrupted.status, 504);
  assert.deepEqual(interrupted.body.errors.code, ['AGENT_TIMEOUT']);
  assert(fs.existsSync(path.join(process.env.ITAM_ARTIFACTS, 'crash-restarted')));
  console.log('PASS 1: killed worker after intent journal, client receives explicit unknown timeout');
  const listed = await op({ kind: 'list', folder: 'Desktop' });
  assert.equal(listed.status, 409, 'New operations must not collide with an unresolved operation');
  assert.deepEqual(listed.body.errors.code, ['OPERATION_IN_PROGRESS']);
  const snapshot = await request(route + '/profile', { token, session });
  assert.equal(snapshot.status, 200);
  assert.equal(snapshot.body.data.quota.usedFiles, 1);
  assert.equal(snapshot.body.data.quota.reservedFiles, 1);
  assert.equal(snapshot.body.data.files.length, 1);
  console.log('PASS 2: new operation returns business conflict, snapshot preserves unknown reservation');
  const retry = await op(mutation);
  assert.equal(retry.status, 409);
  assert.deepEqual(retry.body.errors.code, ['OPERATION_IN_PROGRESS']);
  const another = await op({ kind: 'create', folder: 'Desktop', name: 'extra.txt', contentBase64: '' });
  assert.equal(another.status, 409);
  assert.deepEqual(another.body.errors.code, ['FILE_COUNT_QUOTA_EXCEEDED']);
  console.log('PASS 3: retry cannot duplicate file; new request cannot bypass unknown quota reservation');
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const page = await browser.newPage({ viewport: { width: 1365, height: 900 } });
  page.setDefaultTimeout(20000);
  try {
    await page.goto(process.env.ITAM_UI_URL + '/admin/quotas?demo=0');
    await page.getByLabel('Tên đăng nhập', { exact: true }).fill('admin');
    await page.getByLabel('Mật khẩu', { exact: true }).fill(process.env.ITAM_TEST_PASSWORD);
    await page.getByRole('button', { name: /Đăng nhập$/ }).click();
    await page.getByRole('button', { name: 'Đối soát', exact: true }).click();
    const reconciled = page.waitForResponse(r => r.url().endsWith('/reconcile') && r.request().method() === 'POST');
    await page.getByRole('button', { name: 'Yêu cầu đối soát', exact: true }).click();
    assert.equal((await reconciled).status(), 200);
    await page.getByText('Đối soát hoàn tất. Quota đã được cập nhật theo file hiện có.', { exact: true }).waitFor();
    const after = await request(route + '/profile', { token, session });
    assert.equal(after.body.data.quota.reservedFiles, 0);
    assert.equal(after.body.data.quota.usedFiles, 1);
    assert.equal(after.body.data.files[0].name, 'kept.txt');
    const replay = await op(mutation);
    assert.equal(replay.status, 409);
    assert.deepEqual(replay.body.errors.code, ['OPERATION_RECONCILED']);
    console.log('PASS 4: Admin UI reconciliation releases reservation, preserves files, refuses original replay');
    assert.equal((await op({ kind: 'create', folder: 'Desktop', name: 'after-reconcile.txt', contentBase64: '' })).status, 200);
    await page.getByRole('button', { name: 'Nhật ký đối soát', exact: true }).click();
    await page.getByRole('dialog').getByRole('cell', { name: 'Hoàn tất đối soát', exact: true }).waitFor();
    await page.getByRole('dialog').getByRole('cell', { name: 'Yêu cầu đối soát', exact: true }).waitFor();
    assert.equal(await page.getByText('keep this document', { exact: true }).count(), 0);
    await page.screenshot({ path: path.join(process.env.ITAM_ARTIFACTS, 'reconciliation-admin.png'), fullPage: true, animations: 'disabled' });
    await page.getByRole('dialog').getByRole('button', { name: 'Đóng', exact: true }).click();
    await page.setViewportSize({ width: 390, height: 844 });
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'Quota page overflows on mobile');
    await page.screenshot({ path: path.join(process.env.ITAM_ARTIFACTS, 'reconciliation-mobile.png'), fullPage: true, animations: 'disabled' });
    console.log('PASS 5: audit visible to Admin, new employee operation succeeds, no file content shown');
  } catch (error) {
    await page.screenshot({ path: path.join(process.env.ITAM_ARTIFACTS, 'reconciliation-failure.png'), fullPage: true });
    throw error;
  } finally { await browser.close(); }
})().catch(error => { console.error(redactError(error)); process.exitCode = 1; });
