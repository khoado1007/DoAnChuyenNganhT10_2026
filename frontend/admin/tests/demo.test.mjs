import test from 'node:test';
import assert from 'node:assert/strict';
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
  assert.equal((await an.call(`/machines/${pc1}/files/operations`, { kind: 'read', fileId, requestId: crypto.randomUUID() }, 'POST', other.sessionId)).status, 404);
  await an.call(`/machines/${pc1}/logout`, {}, 'POST', other.sessionId);
  const same = (await khoa.call(`/machines/${pc1}/sessions`, {})).data;
  assert.equal((await khoa.call(`/machines/${pc1}/profile`, null, 'GET', same.sessionId)).data.files.length, 10);
  const second = (await khoa.call(`/machines/${pc2}/sessions`, {})).data;
  assert.equal((await khoa.call(`/machines/${pc2}/profile`, null, 'GET', second.sessionId)).data.quota.usedFiles, 0);
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
