import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Descriptions, Form, Input, Modal, Spin, Typography } from 'antd';
import { api, clearSession, getCurrentUser, getToken } from '../api/auth';
import { isDemoMode } from '../demo/mode';
import type { UserResponse } from '../types/auth';

type Values = { currentPassword: string; newPassword: string; confirmPassword: string };

export default function EmployeeAccountModal({ onClose, onPasswordChanged }: { onClose: () => void; onPasswordChanged: () => void }) {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [form] = Form.useForm<Values>();
  const mounted = useRef(true);
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setUser(null); setError('');
    getCurrentUser(controller.signal)
      .then(value => { if (!controller.signal.aborted) setUser(value); })
      .catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [attempt]);

  async function changePassword(values: Values) {
    if (saving || isDemoMode) return;
    const changingToken = getToken();
    setSaving(true); setError('');
    try {
      await api('/auth/change-password', { method: 'POST', body: JSON.stringify(values) });
      if (getToken() !== changingToken) return;
      clearSession();
      if (!mounted.current) return;
      form.resetFields();
      onPasswordChanged();
    } catch (cause) {
      if (mounted.current) setError((cause as Error).message);
    } finally { if (mounted.current) setSaving(false); }
  }

  return <Modal title="Thông tin tài khoản" open footer={null} onCancel={() => { if (!saving) onClose(); }} closable={!saving} maskClosable={!saving} keyboard={!saving}>
    {loading ? <Spin aria-label="Đang tải tài khoản" /> : <>
      {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} action={!user && <Button onClick={() => setAttempt(value => value + 1)}>Thử lại</Button>} />}
      {user && <>
        <Descriptions column={1} bordered size="small" items={[
          { key: 'name', label: 'Họ và tên', children: user.fullName },
          { key: 'username', label: 'Tên đăng nhập', children: user.username },
          { key: 'email', label: 'Email', children: user.email },
          { key: 'department', label: 'Phòng ban', children: user.department || 'Chưa khai báo' },
          { key: 'status', label: 'Trạng thái', children: user.status === 'Active' ? 'Hoạt động' : user.status },
        ]} />
        <Typography.Title level={4}>Đổi mật khẩu</Typography.Title>
        <Typography.Paragraph>Ít nhất 8 ký tự, có chữ thường và chữ số. Đổi thành công sẽ yêu cầu đăng nhập lại trên các máy. Lưu tài liệu đang soạn trước khi đổi.</Typography.Paragraph>
        {isDemoMode && <Alert type="info" message="Đổi mật khẩu chỉ có khi sử dụng API thật." />}
        {user.role === 'Employee' && <Form form={form} layout="vertical" onFinish={changePassword} disabled={saving || isDemoMode}>
          <Form.Item name="currentPassword" label="Mật khẩu hiện tại" rules={[{ required: true, message: 'Nhập mật khẩu hiện tại.' }]}><Input.Password maxLength={256} autoComplete="current-password" /></Form.Item>
          <Form.Item name="newPassword" label="Mật khẩu mới" dependencies={['currentPassword']} rules={[
            { required: true, min: 8, message: 'Mật khẩu mới cần ít nhất 8 ký tự.' },
            { pattern: /(?=.*[a-z])(?=.*[0-9])/, message: 'Mật khẩu cần có chữ thường và chữ số.' },
            ({ getFieldValue }) => ({ validator: (_, value) => !value || value !== getFieldValue('currentPassword') ? Promise.resolve() : Promise.reject(new Error('Mật khẩu mới phải khác mật khẩu hiện tại.')) }),
          ]}><Input.Password maxLength={256} autoComplete="new-password" /></Form.Item>
          <Form.Item name="confirmPassword" label="Nhập lại mật khẩu mới" dependencies={['newPassword']} rules={[
            { required: true, message: 'Nhập lại mật khẩu mới.' },
            ({ getFieldValue }) => ({ validator: (_, value) => !value || value === getFieldValue('newPassword') ? Promise.resolve() : Promise.reject(new Error('Mật khẩu nhập lại không khớp.')) }),
          ]}><Input.Password maxLength={256} autoComplete="new-password" /></Form.Item>
          <Button type="primary" htmlType="submit" loading={saving}>Đổi mật khẩu</Button>
        </Form>}
      </>}
    </>}
  </Modal>;
}
