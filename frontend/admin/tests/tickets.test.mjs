import test from 'node:test';
import assert from 'node:assert/strict';

test('ticket status follows the supported admin flow only', async () => {
  const {nextTicketAction} = await import('../src/tickets.ts');
  assert.deepEqual(nextTicketAction('Open'), {status: 'InProgress', label: 'Bắt đầu xử lý', needsResolution: false});
  assert.deepEqual(nextTicketAction('InProgress'), {status: 'Resolved', label: 'Hoàn tất xử lý', needsResolution: true});
  assert.deepEqual(nextTicketAction('Resolved'), {status: 'Closed', label: 'Đóng ticket', needsResolution: false});
  for (const status of ['Closed', 'Assigned', 'Cancelled']) assert.equal(nextTicketAction(status), null);
});

test('ticket labels cover every status, priority and category', async () => {
  const {statusLabel, priorityLabel, categoryLabel, statusColor, priorityColor} = await import('../src/tickets.ts');
  assert.equal(statusLabel('Open'), 'Đang mở');
  assert.equal(statusLabel('InProgress'), 'Đang xử lý');
  assert.equal(statusLabel('Resolved'), 'Đã xử lý');
  assert.equal(statusLabel('Closed'), 'Đã đóng');
  assert.equal(statusColor('Closed'), 'default');
  assert.equal(priorityLabel('Critical'), 'Khẩn cấp');
  assert.equal(priorityColor('High'), 'orange');
  assert.equal(categoryLabel('Network'), 'Mạng');
  assert.equal(categoryLabel('Other'), 'Khác');
});

test('ticket timestamps are treated as UTC before formatting', async () => {
  const {asUtcIso, formatTicketTime} = await import('../src/tickets.ts');
  assert.equal(asUtcIso('2026-11-23T01:30:00'), '2026-11-23T01:30:00Z');
  assert.equal(asUtcIso('2026-11-23T01:30:00Z'), '2026-11-23T01:30:00Z');
  assert.equal(asUtcIso('2026-11-23T01:30:00+07:00'), '2026-11-23T01:30:00+07:00');
  assert.equal(typeof formatTicketTime('2026-11-23T01:30:00'), 'string');
});

test('demo ticket filtering matches search and selected facets', async () => {
  const {matchesQuery} = await import('../src/tickets.ts');
  const ticket = {id: '1', ticketNumber: 'TKT-2026-0001', title: 'Máy không kết nối mạng', category: 'Network',
    priority: 'High', status: 'Open', createdByName: 'Trần Đăng Khoa', createdAt: '', updatedAt: null};
  assert.equal(matchesQuery(ticket, {search: 'kết nối'}), true);
  assert.equal(matchesQuery(ticket, {search: 'tkt-2026'}), true);
  assert.equal(matchesQuery(ticket, {search: 'không có'}), false);
  assert.equal(matchesQuery(ticket, {status: 'Open', category: 'Network', priority: 'High'}), true);
  assert.equal(matchesQuery(ticket, {status: 'Closed'}), false);
  assert.equal(matchesQuery(ticket, {priority: 'Low'}), false);
});
