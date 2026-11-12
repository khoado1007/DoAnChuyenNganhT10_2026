import { useState } from 'react';
import { Alert, Button, Form, Input, Typography } from 'antd';
import { CheckCircleOutlined, LoginOutlined, UserOutlined } from '@ant-design/icons';
import { login } from '../api/auth';
import { ApiError } from '../api/client';
import type { LoginRequest, UserResponse } from '../types/auth';

const { Text, Title } = Typography;

/** Hiển thị biểu mẫu đăng nhập và chuyển hồ sơ hợp lệ về component cha. */
export default function LoginPage({ onLogin, isAdminRoute }: { onLogin: (user: UserResponse) => void; isAdminRoute: boolean }) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [form] = Form.useForm<LoginRequest>();

  // Xác thực thông tin, cập nhật lỗi tổng quát và gắn lỗi API vào đúng trường biểu mẫu.
  async function submit(values: LoginRequest) {
    if (loading) return;
    setLoading(true);
    setError('');
    try { onLogin(await login(values)); }
    catch (cause) {
      setError(cause instanceof ApiError ? cause.message : 'Không kết nối được máy chủ. Vui lòng thử lại.');
      if (cause instanceof ApiError && cause.errors) {
        const fields: (keyof LoginRequest)[] = ['username', 'password'];
        form.setFields(fields.filter(name => cause.errors?.[name])
          .map(name => ({ name, errors: cause.errors![name] })));
      }
    } finally { setLoading(false); }
  }

  return <main className="login-page">
    <section className="login-brand-panel">
      <div className="brand-lockup"><span className="brand-mark logo-mark"><img className="brand-logo" src="/brand/stu-logo.png" alt="STU" /></span><span>IT <b>Desk</b></span></div>
      <div className="login-brand-copy"><Text className="eyebrow inverse">{isAdminRoute ? 'CỔNG QUẢN TRỊ NỘI BỘ' : 'CỔNG NHÂN VIÊN'}</Text><Title>{'Công nghệ vận hành\nliền mạch.'.normalize('NFC')}</Title><Text className="login-subtitle">Quản lý thiết bị, tài khoản và yêu cầu hỗ trợ tại một nơi.</Text></div>
      <Text className="login-footnote">IT Asset &amp; Service Management</Text>
    </section>
    <section className="login-form-panel"><div className="login-form-wrap">
      <span className="login-icon"><UserOutlined /></span><Text className="eyebrow">{isAdminRoute ? 'KHU VỰC QUẢN TRỊ' : 'KHU VỰC NHÂN VIÊN'}</Text>
      <Title level={2}>Chào mừng trở lại</Title><Text className="muted login-intro">Đăng nhập bằng tài khoản nội bộ được cấp.</Text>
      {error && <Alert className="login-preview-alert" type="error" showIcon message={error} role="alert" />}
      <Form form={form} layout="vertical" onFinish={submit} disabled={loading} requiredMark={false}>
        <Form.Item name="username" label="Tên đăng nhập" rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập tên đăng nhập.' }]}><Input autoComplete="username" autoFocus size="large" /></Form.Item>
        <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Vui lòng nhập mật khẩu.' }]}><Input.Password autoComplete="current-password" size="large" /></Form.Item>
        <Button type="primary" size="large" htmlType="submit" block loading={loading} icon={<LoginOutlined />}>Đăng nhập</Button>
      </Form>
      <div className="login-secure"><CheckCircleOutlined /> {isAdminRoute ? 'Tài khoản Admin được khởi tạo nội bộ' : 'Đăng nhập bằng tài khoản được cấp'}</div>
    </div><Text className="login-copyright">© {new Date().getFullYear()} IT Desk · Hệ thống quản lý CNTT nội bộ</Text></section>
  </main>;

}
