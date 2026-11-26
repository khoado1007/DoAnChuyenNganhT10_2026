import test from 'node:test';
import assert from 'node:assert/strict';
import { webcrypto } from 'node:crypto';
import { createDemoFetch } from '../src/demo/server.ts';

function client(fetcher) {
  let token = '';
  return {
    async call(path, body, method = body ? 'POST' : 'GET', session) {
      const response = await fetcher(`/api${path}`, { method, headers: { Authorization: `Bearer ${token}`, ...(session ? { 'X-Device-Session': session } : {}) }, ...(body ? { body: JSON.stringify(body) } : {}) });
      const payload = await response.json();
      return { status: response.status, data: payload.data };
    },
    async login(username) {
      const response = await this.call('/auth/login', { username, password: 'Demo@12345' });
      assert.equal(response.status, 200);
      token = response.data.accessToken;
    },
  };
}

test('demo bulk software skips duplicate name and kind and preserves configuration', async () => {
  const fetcher = createDemoFetch(), admin = client(fetcher), employee = client(fetcher);
  await admin.login('admin'); await employee.login('khoa');
  const first = await admin.call('/assets/pc-001/specification', { operatingSystem: 'Keep OS', software: [{ name: 'Editor', kind: 'App', version: 'old', filePath: 'private' }] }, 'PUT');
  const payload = { assetIds: ['pc-001', 'pc-002'], software: [{ name: ' editor ', kind: 'App', version: 'new' }, { name: 'Backup', kind: 'Service' }] };
  assert.equal((await employee.call('/assets/software/bulk', payload)).status, 403);
  const result = await admin.call('/assets/software/bulk', payload);
  assert.equal(result.status, 200);
  assert.equal(result.data.added, 3); assert.equal(result.data.skipped, 1);
  const current = (await admin.call('/assets/pc-001/specification')).data;
  assert.equal(current.operatingSystem, 'Keep OS');
  assert.equal(current.software[0].id, first.data.software[0].id);
  assert.equal(current.software[0].version, 'old');
  assert.equal((await admin.call('/assets/pc-001/specification', { ...first.data, software: [] }, 'PUT')).status, 409);
  assert.equal((await admin.call('/assets/software/bulk', { ...payload, assetIds: ['pc-001', 'missing'] })).status, 404);
  assert.equal((await admin.call('/assets/pc-001/specification')).data.software.length, 2);
});

test('demo scoped software approval keeps employee session and quota without exposing paths', async () => {
  const fetcher = createDemoFetch(), admin = client(fetcher), employee = client(fetcher);
  await admin.login('admin'); await employee.login('khoa');
  const spec = (await admin.call('/assets/pc-001/specification', { software: [{ name: 'Editor', kind: 'App', filePath: 'private' }] }, 'PUT')).data;
  const session = (await employee.call('/machines/pc-001/sessions', {})).data;
  const before = (await employee.call('/machines/pc-001/profile', null, 'GET', session.sessionId)).data;
  const info = (await employee.call('/machines/pc-001/information', null, 'GET', session.sessionId)).data;
  assert.equal(info.software[0].filePath, null);
  const path = `/machines/pc-001/software/${spec.software[0].id}/remove`;
  const body = { adminUsername: 'admin', adminPassword: 'Demo@12345', revision: spec.revision };
  assert.equal((await employee.call(path, body, 'POST', 'wrong-session')).status, 403);
  assert.equal((await employee.call(path, { ...body, adminPassword: 'wrong' }, 'POST', session.sessionId)).status, 403);
  assert.equal((await employee.call(path, { ...body, revision: 'old' }, 'POST', session.sessionId)).status, 409);
  assert.equal((await employee.call(path, body, 'POST', session.sessionId)).status, 200);
  const after = (await employee.call('/machines/pc-001/profile', null, 'GET', session.sessionId)).data;
  assert.deepEqual(after.quota, before.quota);
  assert.equal((await employee.call('/auth/me')).data.role, 'Employee');
  assert.equal((await employee.call('/machines/pc-001/information', null, 'GET', session.sessionId)).data.software.length, 0);
  assert.equal((await employee.call(path, body, 'POST', session.sessionId)).status, 409);
});

test('demo active employees use any active computer and see session-bound simulated specifications', async () => {
  const fetcher = createDemoFetch(), employee = client(fetcher), admin = client(fetcher);
  await employee.login('khoa'); await admin.login('admin');
  const result = await employee.call('/machines/pc-003/sessions', {});
  assert.equal(result.status, 200);
  const specification = { operatingSystem: 'Test OS', software: [{ name: 'Editor', version: '1', filePath: 'C:\\Demo\\editor.exe' }] };
  assert.equal((await admin.call('/assets/pc-003/specification', specification, 'PUT')).status, 200);
  assert.equal((await employee.call('/machines/pc-003/information', null, 'GET', 'wrong')).status, 403);
  const info = await employee.call('/machines/pc-003/information', null, 'GET', result.data.sessionId);
  assert.equal(info.data.operatingSystem, 'Test OS');
  assert.equal(info.data.status, 'simulated');
  assert.equal(info.data.software[0].name, 'Editor');
  assert.equal((await employee.call('/assets/pc-003/specification', specification, 'PUT')).status, 403);
});

test('demo supports per-user/per-machine isolation, quota, persistence across sign out, and revocation', async () => {
  const fetcher = createDemoFetch();
  const khoa = client(fetcher), an = client(fetcher), admin = client(fetcher);
  const machines = (await khoa.call('/machines')).data;
  assert.equal(machines.length, 3);
  const pc1 = machines[0].id, pc2 = machines[1].id;
  await khoa.login('COMPANY\\khoa');
  const session = (await khoa.call(`/machines/${pc1}/sessions`, {})).data;
  const operate = (op) => khoa.call(`/machines/${pc1}/files/operations`, { requestId: crypto.randomUUID(), ...op }, 'POST', session.sessionId);
  let fileId;
  for (let index = 0; index < 10; index++) {
    const result = await operate({ kind: 'create', folder: 'Desktop', name: `note${index}.txt`, contentBase64: btoa('hello') });
    assert.equal(result.status, 200);
    fileId ??= result.data.files[0].fileId;
  }
  assert.equal((await operate({ kind: 'create', folder: 'Desktop', name: 'extra.txt', contentBase64: '' })).status, 409);
  const edit = await operate({ kind: 'write', fileId, contentBase64: btoa('updated') });
  assert.equal(edit.data.quota.usedFiles, 10);
  await khoa.call(`/machines/${pc1}/logout`, {}, 'POST', session.sessionId);
  await an.login('COMPANY\\an');
  const other = (await an.call(`/machines/${pc1}/sessions`, {})).data;
  const empty = await an.call(`/machines/${pc1}/profile`, null, 'GET', other.sessionId);
  assert.equal(empty.data.files.length, 0);
  const details = (await an.call(`/machines/${pc1}/file-details`, null, 'GET', other.sessionId)).data;
  assert.equal(details.files.totalCount, 10);
  assert.equal(details.history.totalCount, 11);
  assert.equal(details.history.items[0].fileName, 'note0.txt');
  assert.deepEqual(Object.keys(details.files.items[0]).sort(), ['extension', 'folder', 'name', 'owner', 'sizeBytes']);
  assert.equal((await an.call(`/machines/${pc1}/files/operations`, { kind: 'read', fileId, requestId: crypto.randomUUID() }, 'POST', other.sessionId)).status, 404);
  await an.call(`/machines/${pc1}/logout`, {}, 'POST', other.sessionId);
  const same = (await khoa.call(`/machines/${pc1}/sessions`, {})).data;
  assert.equal((await khoa.call(`/machines/${pc1}/profile`, null, 'GET', same.sessionId)).data.files.length, 10);
  const second = (await khoa.call(`/machines/${pc2}/sessions`, {})).data;
  assert.equal(second.files.length, 0);
  assert.equal(second.quota.usedFiles, 10);
  assert.equal(second.quota.remainingFiles, 0);
  assert.equal((await khoa.call(`/machines/${pc2}/files/operations`, { kind: 'create', name: 'extra.txt', requestId: crypto.randomUUID() }, 'POST', second.sessionId)).status, 409);
  assert.equal((await khoa.call(`/machines/${pc2}/file-details`, null, 'GET', second.sessionId)).data.history.totalCount, 0);
  await admin.login('admin');
  const users = (await admin.call('/users')).data.items;
  const owner = users.find(x => x.username === 'khoa');
  await admin.call(`/assets/${pc1}/access/${owner.id}`, null, 'DELETE');
  assert.equal((await khoa.call(`/machines/${pc1}/profile`, null, 'GET', same.sessionId)).status, 403);
});

test('demo cannot fall through to a real API and invalid login is rejected', async () => {
  const fetcher = createDemoFetch();
  const response = await fetcher('/api/auth/login', { method: 'POST', body: JSON.stringify({ username: 'admin', password: 'wrong' }) });
  assert.equal(response.status, 401);
  assert.equal((await fetcher('/api/not-implemented')).status, 404);
});

test('demo machine login works when crypto.randomUUID is unavailable', async () => {
  const originalCrypto = Object.getOwnPropertyDescriptor(globalThis, 'crypto');
  Object.defineProperty(globalThis, 'crypto', {
    configurable: true,
    value: { getRandomValues: webcrypto.getRandomValues.bind(webcrypto) },
  });
  try {
    const fetcher = createDemoFetch();
    const login = await fetcher('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ username: 'khoa', password: 'Demo@12345' }),
    });
    const { data: { accessToken } } = await login.json();
    const session = await fetcher('/api/machines/pc-001/sessions', {
      method: 'POST',
      headers: { Authorization: `Bearer ${accessToken}` },
      body: '{}',
    });
    assert.equal(session.status, 200);
  } finally {
    if (originalCrypto) Object.defineProperty(globalThis, 'crypto', originalCrypto);
    else Reflect.deleteProperty(globalThis, 'crypto');
  }
});

test('demo enforces session binding and idempotency for duplicate creates', async () => {
  const fetcher = createDemoFetch(); const user = client(fetcher);
  await user.login('khoa');
  const machine = (await user.call('/machines')).data[0].id;
  const session = (await user.call(`/machines/${machine}/sessions`, {})).data;
  const operation = { kind: 'create', name: 'desktop.ini', folder: 'Desktop', requestId: crypto.randomUUID(), contentBase64: '' };
  assert.equal((await user.call(`/machines/${machine}/files/operations`, operation, 'POST', 'wrong')).status, 403);
  const first = await user.call(`/machines/${machine}/files/operations`, operation, 'POST', session.sessionId);
  const replay = await user.call(`/machines/${machine}/files/operations`, operation, 'POST', session.sessionId);
  assert.equal(first.data.quota.usedFiles, 1);
  assert.equal(replay.data.quota.usedFiles, 1);
});
