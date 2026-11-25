import { deviceSessions } from '../api/auth';
import MyAssets from './MyAssets';
import EmployeeTickets from './EmployeeTickets';
import TechnicianTickets from './TechnicianTickets';
import { Button, Card, Descriptions, Typography } from 'antd';
import type { UserResponse } from '../types/auth';
/** Hiển thị hồ sơ và các chức năng cổng thông tin phù hợp với vai trò. */
export default function PortalPage({ user, onLogout, navigate }: { user: UserResponse; onLogout: () => void; navigate: (path:string) => void }) {
  const saved = deviceSessions.read();
  const machinePath = saved ? `/machines/${saved.deviceId}/desktop` : "/employee";
  return <main className="page-content" style={{ maxWidth: 1000, margin: '0 auto' }}>
    <div className="portal-brand"><img src="/brand/stu-logo.png" alt="STU" /><span>IT <b>Desk</b><small>HỆ THỐNG QUẢN LÝ CNTT</small></span></div>
    <div className="page-heading"><Typography.Title level={1}>{user.role === 'Employee' ? 'Cổng nhân viên' : 'Cổng kỹ thuật viên'}</Typography.Title><Button onClick={onLogout}>Đăng xuất</Button></div>
    <Card title="Hồ sơ của tôi"><Descriptions column={1} items={[
      { key: 'name', label: 'Họ và tên', children: user.fullName },
      { key: 'username', label: 'Tên đăng nhập', children: user.username },
      { key: 'email', label: 'Email', children: user.email },
      { key: 'department', label: 'Phòng ban', children: user.department || 'Chưa cập nhật' },
      { key: 'role', label: 'Vai trò', children: user.role === 'Employee' ? 'Nhân viên' : 'Kỹ thuật viên' },
    ]} /></Card>
    {user.role === 'Employee' && <>
      <Card title="Máy làm việc của tôi" style={{ marginTop: 16 }}>
        <Typography.Paragraph>
          {saved ? "Phiên làm việc trên máy đã chọn đang mở." : "Chọn máy công ty để mở Desktop và Documents của bạn."}
          Tài liệu và quota được lưu riêng theo từng tài khoản và từng máy.
        </Typography.Paragraph>
        <Button type="primary" href={machinePath} onClick={event => { if (!event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) {event.preventDefault(); navigate(machinePath);} }}>{saved ? "Trở về Desktop" : "Chọn máy làm việc"}</Button>
      </Card>
      <MyAssets />
      <EmployeeTickets />
    </>}
    {user.role === 'Technician' && <TechnicianTickets />}
  </main>;
}

