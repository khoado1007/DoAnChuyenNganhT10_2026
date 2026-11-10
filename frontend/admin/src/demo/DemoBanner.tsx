import { useState } from 'react';
import { clearSession, login } from '../api/auth';
import { isDemoMode } from './mode';
import './demo.css';

export default function DemoBanner() {
  const [busy, setBusy] = useState(false);
  if (!isDemoMode) return null;
  async function openAdmin() {
    setBusy(true);
    try {
      const user = await login({ username: 'admin', password: 'Demo@12345' });
      window.dispatchEvent(new CustomEvent('itdesk:preview-user', { detail: user }));
      window.history.pushState(null, '', '/admin'); window.dispatchEvent(new PopStateEvent('popstate'));
    } finally { setBusy(false); }
  }
  function machines() {
    clearSession();
    window.dispatchEvent(new CustomEvent('itdesk:preview-user', { detail: null }));
    window.history.pushState(null, '', '/machines'); window.dispatchEvent(new PopStateEvent('popstate'));
  }
  return <aside className="demo-banner" aria-label="Chế độ xem trước">
    <div><strong>DEMO GIAO DIỆN</strong><span>Dữ liệu trong bộ nhớ · Tải lại trang sẽ đặt lại · Chưa lưu qua Agent</span></div>
    <div className="demo-actions"><button disabled={busy} onClick={() => void openAdmin()}>Admin</button><button onClick={machines}>Mô phỏng máy</button><a href="/login?demo=0">Kết nối API thật ↗</a></div>
  </aside>;
}
