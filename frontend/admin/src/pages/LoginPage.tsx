import { useState, type ReactNode } from 'react';
import { Alert, Button, Form, Input, Typography } from 'antd';
import { CheckCircleOutlined, LoginOutlined, SafetyCertificateOutlined, UserOutlined } from '@ant-design/icons';
import { clearSession, login } from '../api/auth';
import { canUseLoginPortal } from '../routing';
import { ApiError } from '../api/client';
import type { LoginRequest, UserResponse } from '../types/auth';

const { Text, Title } = Typography;

/** Hiển thị biểu mẫu đăng nhập và chuyển hồ sơ hợp lệ về component cha. */
type LoginProps = {
  onLogin: (user: UserResponse) => void | Promise<void>;
  isAdminRoute: boolean;
  machine?: { name: string; assetCode?: string };
  signedInEmployee?: UserResponse;
  onChooseMachine?: () => void;
  notice?: ReactNode;
};
export default function LoginPage({ onLogin, isAdminRoute, machine, signedInEmployee, onChooseMachine, notice }: LoginProps) {
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [form] = Form.useForm<LoginRequest>();

  // Xác thực thông tin, cập nhật lỗi tổng quát và gắn lỗi API vào đúng trường biểu mẫu.
  async function submit(values?: LoginRequest) {
    if (loading) return;
    setLoading(true);
    setError('');
    try {
      const user = values ? await login(values) : signedInEmployee;
      if (!user) return;
      if (!canUseLoginPortal(isAdminRoute, user.role) || (machine && user.role !== 'Employee')) {
        clearSession();
        setError('Tài khoản không có quyền đăng nhập tại cổng này.');
        return;
      }
      await onLogin(user);
    }
    catch (cause) {
      setError(cause instanceof ApiError ? cause.message : 'Không kết nối được máy chủ. Vui lòng thử lại.');
      if (cause instanceof ApiError && cause.errors) {
        const fields: (keyof LoginRequest)[] = ['username', 'password'];
        form.setFields(fields.filter(name => cause.errors?.[name])
          .map(name => ({ name, errors: cause.errors![name] })));
      }
    } finally { setLoading(false); }
  }

  const loginForm = <>
    {error && <Alert className="login-preview-alert" type="error" showIcon message={error} role="alert" />}
    <Form form={form} layout="vertical" onFinish={submit} disabled={loading} requiredMark={false}>
      <Form.Item name="username" label={machine ? "Domain ID" : "Tên đăng nhập"} rules={[{ required: true, whitespace: true, message: 'Vui lòng nhập tên đăng nhập.' }]}><Input autoComplete="username" autoFocus size="large" /></Form.Item>
      <Form.Item name="password" label="Mật khẩu" rules={[{ required: true, message: 'Vui lòng nhập mật khẩu.' }]}><Input.Password autoComplete="current-password" size="large" /></Form.Item>
      <Button type="primary" size="large" htmlType="submit" block loading={loading} icon={<LoginOutlined aria-hidden="true" />}>{machine ? "Đăng nhập máy" : "Đăng nhập"}</Button>
    </Form>
  </>;

  if (isAdminRoute) return <main className="admin-login-page">
    <section className="admin-login-card" aria-labelledby="admin-login-title">
      <div className="admin-login-brand"><img src="/brand/stu-logo.png" alt="STU" /><span>IT <b>Desk</b></span></div>
      <span className="login-icon"><SafetyCertificateOutlined /></span>
      <Text className="eyebrow">CỔNG QUẢN TRỊ NỘI BỘ</Text>
      <Title level={2} id="admin-login-title">Đăng nhập quản trị</Title>
      <Text className="muted login-intro">Truy cập bằng tài khoản quản trị được cấp.</Text>
      {loginForm}
      <div className="login-secure"><CheckCircleOutlined /> Chỉ dành cho quản trị viên hệ thống</div>
    </section>
    <Text className="admin-login-copyright">© {new Date().getFullYear()} IT Desk · Quản lý CNTT nội bộ</Text>
  </main>;

  return <main className="login-page">
    <section className="login-brand-panel">
      <div className="brand-lockup"><span className="brand-mark logo-mark"><img className="brand-logo" src="/brand/stu-logo.png" alt="STU" /></span><span>IT <b>Desk</b></span></div>
      <div className="login-brand-copy"><Text className="eyebrow inverse">CỔNG NHÂN VIÊN</Text><Title>{'Công nghệ vận hành\nliền mạch.'.normalize('NFC')}</Title><Text className="login-subtitle">Quản lý thiết bị, tài khoản và yêu cầu hỗ trợ tại một nơi.</Text></div>
      <Text className="login-footnote">IT Asset &amp; Service Management</Text>
    </section>
    <section className="login-form-panel"><div className="login-form-wrap">
      <span className="login-icon"><UserOutlined /></span><Text className="eyebrow">KHU VỰC NHÂN VIÊN</Text>
      <Title level={2}>{machine ? machine.name : "Chào mừng trở lại"}</Title>
      {machine && <p>{machine.assetCode && <>Mã tài sản <strong>{machine.assetCode}</strong> · </>}Máy đã chọn</p>}<Text className="muted login-intro">{signedInEmployee ? `Đã đăng nhập với tài khoản ${signedInEmployee.username}.` : "Đăng nhập bằng tài khoản nội bộ được cấp."}</Text>
      {notice}
      {signedInEmployee ? <Button type="primary" block size="large" loading={loading} onClick={() => void submit()}>Mở Desktop</Button> : loginForm}
      {onChooseMachine && <Button type="link" disabled={loading} onClick={onChooseMachine}>Chọn máy khác</Button>}
    </div><Text className="login-copyright">© {new Date().getFullYear()} IT Desk · Hệ thống quản lý CNTT nội bộ</Text></section>
  </main>;

}
