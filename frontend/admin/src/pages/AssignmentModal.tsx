import { useEffect, useState } from 'react';
import { Alert, Button, Form, Input, Modal, Select, Space, Table, Tag } from 'antd';
import { api } from '../api/auth';
import type { AssetResponse } from '../types/asset';
import type { AssignmentResponse } from '../types/assignment';
import type { UserResponse } from '../types/auth';
import type { PagedResult } from '../types/api';

/** Quản lý lịch sử và thao tác cấp phát, trả hoặc thu hồi một tài sản. */
export default function AssignmentModal({ asset, onClose, onChanged }: { asset: AssetResponse; onClose: () => void; onChanged: () => void }) {
  const [currentAsset, setCurrentAsset] = useState(asset);
  const [rows, setRows] = useState<AssignmentResponse[]>([]);
  const [users, setUsers] = useState<UserResponse[]>([]);
  const [search, setSearch] = useState('');
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(true);
  const [userLoading, setUserLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [userError, setUserError] = useState('');
  const [notes, setNotes] = useState('');
  const [form] = Form.useForm<{ userId: string }>();
  useEffect(() => {
    // Tải lịch sử cấp phát và trạng thái mới nhất của tài sản cùng lúc.
    const abort = new AbortController(); setLoading(true); setError('');
    Promise.all([api<AssignmentResponse[]>(`/assets/${asset.id}/assignments`, { signal: abort.signal }), api<AssetResponse>(`/assets/${asset.id}`, { signal: abort.signal })])
      .then(([data, detail]) => { if (!abort.signal.aborted) { setRows(data); setCurrentAsset(detail); } })
      .catch(e => { if (!abort.signal.aborted) setError(e.message); })
      .finally(() => { if (!abort.signal.aborted) setLoading(false); });
    return () => abort.abort();
  }, [asset.id, revision]);
  useEffect(() => {
    // Tải nhân viên đang hoạt động theo truy vấn và bỏ kết quả khi request bị hủy.
    const abort = new AbortController(); setUserLoading(true); setUserError('');
    api<PagedResult<UserResponse>>(`/users?pageSize=100&search=${encodeURIComponent(search)}`, { signal: abort.signal })
      .then(data => { if (!abort.signal.aborted) setUsers(data.items.filter(u => u.role === 'Employee' && u.status === 'Active')); })
      .catch(e => { if (!abort.signal.aborted) setUserError(e.message); })
      .finally(() => { if (!abort.signal.aborted) setUserLoading(false); });
    return () => abort.abort();
  }, [search]);
  const active = rows.find(row => row.status === 'Active');
  // Thực hiện thao tác cấp phát/trả/thu hồi, sau đó tải lại lịch sử và báo trang cha.
  async function mutate(action: 'assign' | 'return' | 'revoke', userId?: string) {
    setSaving(true); setError('');
    try {
      await api(`/assets/${asset.id}/${action}`, { method: 'POST', body: JSON.stringify(action === 'assign' ? { userId, notes } : { assignmentId: active?.id, notes }) });
      setNotes(''); setRevision(r => r + 1); onChanged();
    } catch (e) { setError(e instanceof Error ? e.message : 'Không lưu được cấp phát.'); }
    finally { setSaving(false); }
  }
  return <Modal title={`Cấp phát ${asset.assetCode}`} open onCancel={() => { if (!saving) onClose(); }} footer={null} width={900}>
    {error && <Alert type="error" showIcon message={error} />}
    {userError && <Alert type="error" showIcon message={userError} />}
    <Button style={{ marginBlock: 12 }} disabled={saving} onClick={() => setRevision(r => r + 1)}>Làm mới lịch sử</Button>
    <Table<AssignmentResponse> rowKey="id" dataSource={rows} loading={loading} size="small" scroll={{ x: 780 }} pagination={{ pageSize: 5 }} columns={[
      { title: 'Người nhận', dataIndex: 'username' },
      { title: 'Ngày cấp', dataIndex: 'assignedAt', render: (value: string) => new Date(value).toLocaleString('vi-VN') },
      { title: 'Người cấp', dataIndex: 'assignedBy' },
      { title: 'Trạng thái', dataIndex: 'status', render: (value: AssignmentResponse['status']) => <Tag>{({ Active: 'Đang sử dụng', Returned: 'Đã trả', Revoked: 'Đã thu hồi' })[value]}</Tag> },
      { title: 'Ngày đóng', dataIndex: 'returnedAt', render: (value: string | null) => value ? new Date(value).toLocaleString('vi-VN') : '—' },
      { title: 'Người đóng', dataIndex: 'closedBy' },
      { title: 'Ghi chú cấp', dataIndex: 'notes' }, { title: 'Ghi chú đóng', dataIndex: 'closeNotes' },
    ]} locale={{ emptyText: 'Chưa có lịch sử cấp phát' }} />
    {!loading && <>
      <label htmlFor="assignment-notes">Ghi chú thao tác</label>
      <Input.TextArea id="assignment-notes" maxLength={1000} value={notes} disabled={saving} onChange={event => setNotes(event.target.value)} style={{ marginBlock: 8 }} />
      {active ? <Space wrap><Button loading={saving} onClick={() => mutate('return')}>Xác nhận trả</Button><Button danger loading={saving} onClick={() => mutate('revoke')}>Thu hồi</Button></Space>
        : currentAsset.status === 'Available' ? <Form form={form} layout="vertical" onFinish={values => mutate('assign', values.userId)} disabled={saving}>
          <Input.Search placeholder="Tìm nhân viên theo tên đăng nhập" maxLength={100} onSearch={value => { form.resetFields(); setSearch(value); }} style={{ marginBlock: 12 }} />
          <Form.Item name="userId" label="Người nhận" rules={[{ required: true, message: 'Chọn nhân viên nhận tài sản.' }]}>
            <Select loading={userLoading} options={users.map(user => ({ value: user.id, label: user.username }))} placeholder="Chọn nhân viên đang hoạt động" />
          </Form.Item>
          <Button htmlType="submit" type="primary" loading={saving} disabled={userLoading}>Xác nhận cấp phát</Button>
        </Form> : <Alert type="info" message="Chỉ tài sản sẵn sàng mới có thể cấp phát." />}
    </>}
  </Modal>;
}


