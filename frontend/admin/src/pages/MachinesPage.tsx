import { DesktopOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Space, Tag, Typography } from 'antd';
import { useEffect, useState, type ReactNode } from 'react';
import { api, deviceSessions, getToken } from '../api/auth';
import type { Machine, MachineProfile } from '../api/machines';
import { isDemoMode } from '../demo/mode';
import type { UserResponse } from '../types/auth';
import DesktopFiles from './DesktopFiles';
import EmployeeAccountModal from './EmployeeAccountModal';
import LoginPage from './LoginPage';
import MachineWorkspace from './MachineWorkspace';
import PasswordReminder from './PasswordReminder';
import { useDesktopFiles } from './useDesktopFiles';
import { useMachineSession } from './useMachineSession';

type PageProps = {
  path: string;
  navigate: (path: string, replace?: boolean) => void;
  user: UserResponse | null;
  onUserChange: (user: UserResponse | null) => void;
};

function MachineShell({ actions, children }: { actions?: ReactNode; children: ReactNode }) {
  return <main className={`workstation-shell ${actions ? 'has-session' : ''}`}>
    <Space className="workstation-header" wrap style={{width:'100%', justifyContent:'space-between'}}>
      <Typography.Title level={2}><DesktopOutlined /> Không gian máy công ty</Typography.Title>
      {actions && <Space wrap>{actions}</Space>}
    </Space>
    <Typography.Paragraph className="workstation-description">{isDemoMode
      ? 'Dữ liệu minh họa trong bộ nhớ trình duyệt.'
      : 'Desktop/Documents mô phỏng · Tài liệu được lưu trên máy chủ theo từng tài khoản và máy.'}</Typography.Paragraph>
    {children}
  </main>;
}

function EmployeeDesktop({ initialProfile, session, openTickets }: {
  initialProfile: MachineProfile; session: ReturnType<typeof useMachineSession>; openTickets: () => void;
}) {
  const [accountOpen, setAccountOpen] = useState(false);
  const files = useDesktopFiles(initialProfile, session.returnToLogin);
  const busy = session.leaving || files.busy;
  return <MachineShell actions={<>
    <Button disabled={busy} onClick={openTickets}>Yêu cầu hỗ trợ</Button>
    <Button disabled={busy} onClick={() => void session.leave()}>Đăng xuất / Đổi máy</Button>
  </>}>
    {(session.error || files.error) && <Alert type="error" showIcon message={session.error || files.error} style={{marginBottom:16}}/>}
    <PasswordReminder editing={accountOpen || busy} onChange={() => setAccountOpen(true)}/>
    <Button style={{marginBottom:16}} disabled={busy} onClick={() => setAccountOpen(true)}>Thông tin tài khoản</Button>
    {accountOpen && <EmployeeAccountModal onClose={() => setAccountOpen(false)} onPasswordChanged={session.afterPasswordChange}/>}
    <MachineWorkspace session={initialProfile}>
      <DesktopFiles files={files} leaving={session.leaving} onLeave={() => void session.leave()}/>
    </MachineWorkspace>
  </MachineShell>;
}

export default function MachinesPage({ path, navigate, user, onUserChange }: PageProps) {
  const device = path.startsWith('/machines/') ? path.split('/')[2] : '';
  const session = useMachineSession(device, navigate, onUserChange);
  const [machines, setMachines] = useState<Machine[]>([]);
  const [listStatus, setListStatus] = useState<'loading' | 'ready' | 'error'>('loading');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setListStatus('loading');
    api<Machine[]>('/machines', {signal:controller.signal})
      .then(items => {
        if (!controller.signal.aborted) {
          setMachines(items);
          setListStatus('ready');
        }
      })
      .catch(() => { if (!controller.signal.aborted) setListStatus('error'); });
    return () => controller.abort();
  }, [device, attempt]);

  if (session.restoring || session.restoreError) return <main className="session-loading">
    {session.restoring ? <p role="status">Đang kiểm tra phiên trên máy…</p> : <>
      <Alert type="error" showIcon message={session.restoreError}/><Button onClick={session.retry}>Thử lại</Button>
    </>}
  </main>;
  if (session.profile) return <EmployeeDesktop key={session.profile.sessionId} initialProfile={session.profile}
    session={session} openTickets={() => navigate('/employee/tickets')}/>;

  const listError = listStatus === 'error' && <Alert type="error" showIcon message="Chưa tải được danh sách máy."
    action={<Button onClick={() => setAttempt(value => value + 1)}>Thử lại</Button>}/>;
  if (device) {
    const machine = machines.find(item => item.id === device);
    return <LoginPage key={device} isAdminRoute={false} machine={{name:machine?.name ?? device, assetCode:machine?.assetCode}}
      signedInEmployee={user?.role === 'Employee' && getToken() && !deviceSessions.read() ? user : undefined}
      onLogin={session.enter} onChooseMachine={() => navigate('/employee')} notice={<>
        {session.passwordChanged && <Alert type="success" showIcon message="Đã đổi mật khẩu" description="Hãy đăng nhập lại bằng mật khẩu mới."/>}
        {session.error && <Alert type="error" showIcon message={session.error}/>}
        {listError}
      </>}/>;
  }
  return <MachineShell><section className="machine-chooser">
    <div className="machine-intro"><span>WORKSTATION / 01</span><h1>Chọn nơi làm việc của bạn.</h1>
      <p>Chọn máy trước khi đăng nhập. Tài khoản nhân viên đang hoạt động có thể dùng mọi máy đang hoạt động. Tài liệu lưu theo máy. Quota dùng chung trên tất cả máy của tài khoản.</p>
    </div>
    {listStatus === 'loading' && <p role="status">Đang tải danh sách máy…</p>}
    {listError}
    {listStatus === 'ready' && (machines.length === 0 ? <p role="status">Chưa có máy khả dụng.</p> :
      <div className="machine-grid">{machines.map(machine =>
        <Card className="machine-card" key={machine.id} title={<><DesktopOutlined className="machine-icon"/><span>{machine.name}</span></>}>
          <p>{machine.assetCode} <Tag color="blue">Mô phỏng</Tag></p>
          <Button type="primary" block onClick={() => navigate(`/machines/${machine.id}/login`)}>Chọn máy →</Button>
        </Card>)}</div>)}
  </section></MachineShell>;
}
