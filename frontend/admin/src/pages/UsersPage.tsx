import { isDemoMode } from '../demo/mode';
import { useEffect, useState } from 'react';
import { Alert, Button, Form, Input, Modal, Popconfirm, Select, Space, Table, Tag, Typography } from 'antd';
import type { ColumnsType } from 'antd/es/table';
import { api } from '../api/auth';
import type { UserResponse, UserRole, UserStatus } from '../types/auth';
import type { PagedResult } from '../types/api';
import { ApiError } from '../api/client';

type Values = { username?: string; email: string; fullName: string; department: string; password?: string; role: UserRole; status: UserStatus };
const roles = [{ value: 'Employee', label: 'Nhân viên' }, { value: 'Technician', label: 'Kỹ thuật viên' }];
const statuses = [{ value: 'Active', label: 'Hoạt động' }, { value: 'Locked', label: 'Đã khóa' }, { value: 'Inactive', label: 'Ngừng hoạt động' }];
/** Quản lý danh sách tài khoản, tìm kiếm và biểu mẫu tạo/chỉnh sửa. */
export default function UsersPage() {
  const [data, setData] = useState<PagedResult<UserResponse> | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [revision, setRevision] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [modalError, setModalError] = useState('');
  const [open, setOpen] = useState(false);
  const [editing, setEditing] = useState<UserResponse | null>(null);
  const [saving, setSaving] = useState(false);
  const [resetting, setResetting] = useState<string | null>(null);
  const [notice, setNotice] = useState('');
  const [form] = Form.useForm<Values>();
  useEffect(() => {
    // Tải trang tài khoản theo truy vấn hiện tại và hủy request khi component đổi bộ lọc.
    const controller = new AbortController();
    setLoading(true); setError('');
    api<PagedResult<UserResponse>>(`/users?page=${page}&pageSize=10&search=${encodeURIComponent(search)}`, { signal: controller.signal })
      .then(value => { if (!controller.signal.aborted) setData(value); })
      .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Không tải được tài khoản.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, search, revision]);
  // Chuẩn bị biểu mẫu tạo mới hoặc điền dữ liệu tài khoản được chọn.
  function show(row: UserResponse | null) {
    setEditing(row); setModalError(''); form.resetFields();
    form.setFieldsValue(row ? { ...row } : { role: 'Employee', status: 'Active', department: '' });
    setOpen(true);
  }
  // Tạo hoặc cập nhật tài khoản; gắn lỗi xác thực của máy chủ vào các trường tương ứng.
  async function save(values: Values) {
    setSaving(true); setModalError('');
    try {
      const { email, fullName, department, role, status } = values;
      await api(editing ? `/users/${editing.id}` : '/users', {
        method: editing ? 'PUT' : 'POST',
        body: JSON.stringify(editing ? { email, fullName, department, role, status } : { username: values.username, email, fullName, department: department || '', role, password: '12345Az@' }),
      });
      setOpen(false); form.resetFields(); setRevision(value => value + 1);
    } catch (cause) {
      setModalError(cause instanceof Error ? cause.message : 'Không lưu được tài khoản.');
      if (cause instanceof ApiError && cause.errors) {
        const fields: (keyof Values)[] = ['username', 'email', 'fullName', 'department', 'password', 'role', 'status'];
        form.setFields(fields.filter(name => cause.errors?.[name]).map(name => ({ name, errors: cause.errors![name] })));
      }
    } finally { setSaving(false); }
  }
  async function resetPassword(row: UserResponse) {
    setResetting(row.id); setError(''); setNotice('');
    try { await api(`/users/${row.id}/reset-password`, {method:'POST'}); setNotice(`Đã reset ${row.username} về 12345Az@. Phiên cũ bị thu hồi; trạng thái khóa được giữ nguyên.`); setRevision(x=>x+1); }
    catch (e) { setError((e as Error).message); } finally {setResetting(null);}
  }
  const columns: ColumnsType<UserResponse> = [
    { title: 'Tên đăng nhập', dataIndex: 'username' },
    { title: 'Họ và tên', dataIndex: 'fullName' },
    { title: 'Email', dataIndex: 'email' },
    { title: 'Phòng ban', dataIndex: 'department' },
    { title: 'Vai trò', dataIndex: 'role', render: (role: UserRole) => role === 'Admin' ? 'Quản trị viên' : roles.find(value => value.value === role)?.label },
    { title: 'Trạng thái', dataIndex: 'status', render: (status: UserStatus) => <Tag color={status === 'Active' ? 'green' : 'default'}>{statuses.find(value => value.value === status)?.label}</Tag> },
    { title: 'Thao tác', key: 'actions', render: (_: unknown, row: UserResponse) => <Space wrap><Button disabled={row.role === 'Admin'} onClick={() => show(row)}>Chỉnh sửa</Button><Popconfirm title={`Reset mật khẩu ${row.username}?`} description="Mật khẩu mới12345Az@, thu hồi phiên cũ. Không tự mở khóa tài khoản." okText="Reset" cancelText="Hủy" onConfirm={()=>resetPassword(row)}><Button disabled={row.role==='Admin'||isDemoMode||resetting!==null} loading={resetting===row.id}>Reset mật khẩu</Button></Popconfirm></Space> },
  ];
  return <main className="page-content">
    <div className="page-heading"><div><Typography.Title level={1}>Tài khoản</Typography.Title><Typography.Text>Quản lý nhân viên và kỹ thuật viên · {isDemoMode ? 'Dữ liệu demo' : 'Dữ liệu từ máy chủ'}</Typography.Text></div><Button type="primary" onClick={() => show(null)}>Thêm tài khoản</Button></div>
    {notice && <Alert type="success" showIcon message={notice} style={{marginBottom:16}} />}
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Space style={{ marginBottom: 16 }} wrap><Input.Search aria-label="Tìm tài khoản" placeholder="Tên, email, họ tên" maxLength={100} allowClear onSearch={value => { setSearch(value); setPage(1); }} /><Button onClick={() => setRevision(value => value + 1)}>Làm mới</Button></Space>
    <Table<UserResponse> rowKey="id" columns={columns} dataSource={data?.items ?? []} loading={loading} scroll={{ x: 1000 }} pagination={{ current: page, pageSize: 10, total: data?.totalCount ?? 0, showSizeChanger: false, onChange: setPage }} locale={{ emptyText: 'Chưa có tài khoản phù hợp' }} />
    <Modal title={editing ? 'Chỉnh sửa tài khoản' : 'Thêm tài khoản'} open={open} footer={null} onCancel={() => { if (!saving) { setOpen(false); form.resetFields(); } }} destroyOnClose>
      {modalError && <Alert type="error" showIcon message={modalError} style={{ marginBottom: 12 }} />}
      <Form form={form} layout="vertical" onFinish={save} disabled={saving}>
        {!editing && <Form.Item name="username" label="Tên đăng nhập" rules={[{ required: true, whitespace: true }]}><Input maxLength={100} autoComplete="off" /></Form.Item>}
        <Form.Item name="fullName" label="Họ và tên" rules={[{ required: true, whitespace: true }]}><Input maxLength={200} /></Form.Item>
        <Form.Item name="email" label="Email" rules={[{ required: true }, { type: 'email' }]}><Input maxLength={200} /></Form.Item>
        <Form.Item name="department" label="Phòng ban"><Input maxLength={100} /></Form.Item>
        <Form.Item name="role" label="Vai trò" rules={[{ required: true }]}><Select options={roles} /></Form.Item>
        {!editing && <Alert style={{marginBottom:16}} type="info" message="Mật khẩu ban đầu: 12345Az@" description="Nhân viên có 7 ngày từ lần đăng nhập đầu tiên để đổi mật khẩu; nhắc lại ở ngày 5, 6 và 7." />}
        {editing && <><Form.Item name="status" label="Trạng thái" rules={[{ required: true }]}><Select options={statuses} /></Form.Item><Alert style={{ marginBottom: 16 }} type="info" message="Đổi vai trò hoặc trạng thái sẽ thu hồi các phiên đăng nhập hiện tại. Tài khoản cần đăng nhập lại." /></>}
        <Button type="primary" htmlType="submit" loading={saving}>Lưu tài khoản</Button>
      </Form>
    </Modal>
  </main>;
}
