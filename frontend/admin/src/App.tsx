import { lazy, useEffect, useState } from 'react';
import { Alert, Button, Result, Spin } from 'antd';
import { clearSession, getCurrentUser, getToken, sessionExpiredEvent } from './api/auth';
import { ApiError } from './api/client';
import type { UserResponse } from './types/auth';
import LoginPage from './pages/LoginPage';
const AdminPreviewPage = lazy(() => import('./pages/AdminPreviewPage'));
const MachinesPage = lazy(() => import('./pages/MachinesPage'));
const PortalPage = lazy(() => import('./pages/PortalPage'));
import { destinationAfterLogin, homeForRole, resolveRoute } from './routing';

/** Điều phối phiên đăng nhập, điều hướng theo vai trò và nội dung trang hiện tại. */
export default function App() {
  const [user, setUser] = useState<UserResponse | null>(null);
  const [checking, setChecking] = useState(Boolean(getToken()));
  const [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [path, setPath] = useState(window.location.pathname);
  useEffect(() => {
    const changed = (event: Event) => { setUser((event as CustomEvent<UserResponse | null>).detail); setError(''); setChecking(false); };
    window.addEventListener('itdesk:preview-user', changed);
    return () => window.removeEventListener('itdesk:preview-user', changed);
  }, []);
  // Cập nhật URL và trạng thái React đồng thời; hỗ trợ cả điều hướng mới lẫn thay thế lịch sử.
  function navigate(next: string, replace = false) {
    if (replace) window.history.replaceState(null, '', next);
    else window.history.pushState(null, '', next);
    setPath(next);
  }

  // Xóa phiên cục bộ và đưa người dùng về màn hình đăng nhập.
  function logout() { clearSession(); setUser(null); setError(''); navigate('/login', true); }
  useEffect(() => {
    // Đồng bộ đường dẫn khi người dùng dùng nút quay lại hoặc tiến tới của trình duyệt.
    const changed = () => setPath(window.location.pathname);
    window.addEventListener('popstate', changed);
    return () => window.removeEventListener('popstate', changed);
  }, []);
  useEffect(() => {
    // Đưa người đã đăng nhập ở trang gốc hoặc trang đăng nhập về trang chủ theo vai trò.
    if (user && ['/', '/login', '/login/'].includes(path)) navigate(homeForRole(user.role), true);
  }, [user, path]);
  useEffect(() => {
    const controller = new AbortController();
    // Kết thúc phiên hiển thị ngay khi API thông báo token hiện tại không còn hợp lệ.
    const expired = () => { setUser(null); setError(''); navigate(window.location.pathname.startsWith('/machines') ? '/machines' : '/login', true); };
    window.addEventListener(sessionExpiredEvent, expired);
    // Khôi phục hồ sơ bằng token hiện tại; bỏ qua kết quả cũ nếu phiên đã đổi hoặc yêu cầu bị hủy.
    async function restoreSession() {
      const restoringToken = getToken();
      if (!restoringToken) { setChecking(false); return; }
      setChecking(true); setError('');
      try { const restored = await getCurrentUser(controller.signal); if (!controller.signal.aborted && getToken() === restoringToken) setUser(restored); }
      catch (cause) {
        if (controller.signal.aborted) return;
        if (cause instanceof ApiError && cause.status === 401) setUser(null);
        else setError('Chưa thể kiểm tra phiên đăng nhập. Kiểm tra kết nối máy chủ và thử lại.');
      } finally { if (!controller.signal.aborted) setChecking(false); }
    }
    void restoreSession();
    return () => { controller.abort(); window.removeEventListener(sessionExpiredEvent, expired); };
  }, [attempt]);
  if (path === '/machines' || path.startsWith('/machines/')) return <MachinesPage path={path} navigate={navigate} />;
  if (checking) return <div className="session-loading"><Spin size="large" /><p>Đang kiểm tra phiên đăng nhập…</p></div>;
  if (error) return <div className="session-loading"><Alert type="error" showIcon message={error} /><Button onClick={() => setAttempt(value => value + 1)}>Thử lại</Button><Button type="text" onClick={logout}>Về đăng nhập</Button></div>;
  if (!user) return <LoginPage onLogin={value => { setUser(value); navigate(destinationAfterLogin(path, value.role), true); }} />;
  const route = resolveRoute(['/', '/login', '/login/'].includes(path) ? homeForRole(user.role) : path, user.role);
  if (route.kind === 'forbidden' || route.kind === 'not-found') return <Result status={route.kind === 'forbidden' ? '403' : '404'} title={route.kind === 'forbidden' ? 'Bạn không có quyền truy cập khu vực này' : 'Không tìm thấy trang'} extra={<><Button onClick={() => navigate(homeForRole(user.role), true)}>Về trang của tôi</Button><Button onClick={logout}>Đăng xuất</Button></>} />;
  if (route.kind === 'admin') return <AdminPreviewPage user={user} onLogout={logout} section={route.section} setSection={section => navigate(section === 'overview' ? '/admin' : `/admin/${section}`)} />;
  return <PortalPage user={user} onLogout={logout} />;
}
