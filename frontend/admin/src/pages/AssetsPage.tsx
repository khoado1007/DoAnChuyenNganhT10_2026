import { isDemoMode } from '../demo/mode';
import DeviceAccessModal from './DeviceAccessModal';
import AssignmentModal from './AssignmentModal';
import { useEffect, useState } from 'react';
import { Alert, Button, Form, Input, Modal, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { api } from '../api/auth';
import type { AssetResponse, AssetStatus, AssetType } from '../types/asset';
import type { PagedResult } from '../types/api';

const types: { value: AssetType; label: string }[] = [
  { value: 'Desktop', label: 'Máy tính bàn' }, { value: 'Laptop', label: 'Máy tính xách tay' },
  { value: 'Server', label: 'Máy chủ' }, { value: 'Printer', label: 'Máy in' },
  { value: 'NetworkDevice', label: 'Thiết bị mạng' }, { value: 'Other', label: 'Khác' },
];
const statuses: { value: AssetStatus; label: string }[] = [
  { value: 'Available', label: 'Sẵn sàng' }, { value: 'Assigned', label: 'Đã cấp phát' },
  { value: 'UnderMaintenance', label: 'Bảo trì' }, { value: 'Retired', label: 'Ngừng sử dụng' }, { value: 'Disabled', label: 'Vô hiệu hóa' },
];
type Values = Omit<AssetResponse, 'id' | 'createdAt' | 'updatedAt'>;
/** Quản lý danh mục tài sản, tìm kiếm, cập nhật và các phiên cấp phát. */
export default function AssetsPage() {
  const [access,setAccess] = useState<string | null>(null);
  const [allocation, setAllocation] = useState<AssetResponse | null>(null);
  const [data, setData] = useState<PagedResult<AssetResponse> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState<AssetStatus | undefined>();
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [modalError, setModalError] = useState('');
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<AssetResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [form] = Form.useForm<Values>();
  useEffect(() => {
    // Tải trang tài sản theo bộ lọc hiện tại và hủy request khi điều kiện thay đổi.
    const controller = new AbortController(); setLoading(true); setError('');
    api<PagedResult<AssetResponse>>(`/assets/search?page=${page}&pageSize=10&search=${encodeURIComponent(search)}${status ? `&status=${status}` : ''}`, { signal: controller.signal })
      .then(result => { if (!controller.signal.aborted) setData(result); })
      .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Không tải được tài sản.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, search, status, revision]);
  // Khởi tạo biểu mẫu thêm mới hoặc nạp thông tin của tài sản cần chỉnh sửa.
  function show(row: AssetResponse | null) {
    setEditing(row); setModalError(''); form.resetFields();
    form.setFieldsValue(row ?? { type: 'Desktop', status: 'Available' }); setOpen(true);
  }
  // Lưu tài sản mới hoặc cập nhật tài sản hiện có rồi làm mới danh sách.
  async function save(values: Values) {
    setSaving(true); setModalError('');
    try {
      const { name, type, serialNumber = '', manufacturer = '', model = '', location = '', notes = '' } = values;
      const details = { name, type, serialNumber, manufacturer, model, location, notes };
      await api(editing ? `/assets/${editing.id}` : '/assets', {
        method: editing ? 'PUT' : 'POST', body: JSON.stringify(editing ? { ...details, status: values.status } : { ...details, assetCode: values.assetCode }),
      });
      setOpen(false); form.resetFields(); setRevision(value => value + 1);
    } catch (cause) { setModalError(cause instanceof Error ? cause.message : 'Không lưu được tài sản.'); }
    finally { setSaving(false); }
  }
  const columns: ColumnsType<AssetResponse> = [
    { title: 'Mã tài sản', dataIndex: 'assetCode' }, { title: 'Tên thiết bị', dataIndex: 'name' },
    { title: 'Loại', dataIndex: 'type', render: (value: AssetType) => types.find(type => type.value === value)?.label },
    { title: 'Serial', dataIndex: 'serialNumber' }, { title: 'Vị trí', dataIndex: 'location' },
    { title: 'Vòng đời', dataIndex: 'status', render: (value: AssetStatus) => <Tag>{statuses.find(status => status.value === value)?.label}</Tag> },
    { title: 'Thao tác', key: 'actions', render: (_: unknown, row: AssetResponse) => <Space wrap><Button onClick={() => show(row)}>Chi tiết / sửa</Button><Button onClick={() => setAccess(row.id)}>Quyền đăng nhập</Button><Button onClick={() => setAllocation(row)}>Cấp phát / lịch sử</Button></Space> },
  ];
  return <main className="page-content">
    <div className="page-heading"><div><Typography.Title level={1}>Tài sản</Typography.Title><Typography.Text>Danh mục thiết bị · {isDemoMode ? 'Dữ liệu demo' : 'Dữ liệu từ máy chủ'}</Typography.Text></div><Button type="primary" onClick={() => show(null)}>Thêm tài sản</Button></div>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Space wrap style={{ marginBottom: 16 }}>
      <Input.Search aria-label="Tìm tài sản" placeholder="Mã, tên, serial" maxLength={100} allowClear onSearch={value => { setSearch(value); setPage(1); }} />
      <Select aria-label="Lọc vòng đời" placeholder="Tất cả vòng đời" allowClear style={{ minWidth: 190 }} options={statuses} value={status} onChange={value => { setStatus(value); setPage(1); }} />
      <Button onClick={() => setRevision(value => value + 1)}>Làm mới</Button>
    </Space>
    <Table<AssetResponse> columns={columns} rowKey="id" dataSource={data?.items ?? []} loading={loading} scroll={{ x: 950 }} pagination={{ current: page, pageSize: 10, total: data?.totalCount ?? 0, showSizeChanger: false, onChange: setPage }} locale={{ emptyText: 'Chưa có tài sản phù hợp' }} />
    {access && <DeviceAccessModal deviceId={access} onClose={() => setAccess(null)} />}
    {allocation && <AssignmentModal asset={data?.items.find(row => row.id === allocation.id) ?? allocation} onClose={() => setAllocation(null)} onChanged={() => setRevision(value => value + 1)} />}
    <Modal title={editing ? `Chi tiết ${editing.assetCode}` : 'Thêm tài sản'} open={open} footer={null} destroyOnClose onCancel={() => { if (!saving) setOpen(false); }}>
      {modalError && <Alert type="error" showIcon message={modalError} style={{ marginBottom: 12 }} />}
      <Form form={form} layout="vertical" onFinish={save} disabled={saving}>
        {!editing && <Form.Item name="assetCode" label="Mã tài sản" rules={[{ required: true, whitespace: true }]}><Input maxLength={50} /></Form.Item>}
        <Form.Item name="name" label="Tên thiết bị" rules={[{ required: true, whitespace: true }]}><Input maxLength={200} /></Form.Item>
        <Form.Item name="type" label="Loại thiết bị" rules={[{ required: true }]}><Select options={types} /></Form.Item>
        <Form.Item name="serialNumber" label="Serial"><Input maxLength={100} /></Form.Item>
        <Form.Item name="manufacturer" label="Hãng sản xuất"><Input maxLength={100} /></Form.Item>
        <Form.Item name="model" label="Model"><Input maxLength={100} /></Form.Item>
        <Form.Item name="location" label="Vị trí"><Input maxLength={200} /></Form.Item>
        <Form.Item name="notes" label="Ghi chú"><Input.TextArea maxLength={1000} /></Form.Item>
        {editing && <><Form.Item name="status" label="Vòng đời" rules={[{ required: true }]}><Select disabled={editing.status === 'Assigned' || editing.status === 'Retired'} options={statuses.map(status => ({ ...status, disabled: status.value === 'Assigned' }))} /></Form.Item><Alert type="info" style={{ marginBottom: 16 }} message="Ngừng sử dụng là trạng thái cuối. Cấp phát/trả thiết bị được thực hiện qua luồng riêng; trạng thái kết nối Agent không đổi vòng đời tài sản." /></>}
        <Button type="primary" htmlType="submit" loading={saving}>Lưu tài sản</Button>
      </Form>
    </Modal>
  </main>;
}
