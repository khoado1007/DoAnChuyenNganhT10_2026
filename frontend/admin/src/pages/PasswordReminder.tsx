import { useEffect, useState } from 'react';
import { Alert, Button, Modal, Space } from 'antd';
import { api } from '../api/auth';
import { isDemoMode } from '../demo/mode';

type Policy = { required: boolean; deadline: string | null; reminderDay: number | null };
export default function PasswordReminder({ editing, onChange }: { editing: boolean; onChange: () => void }) {
  const [policy, setPolicy] = useState<Policy | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (isDemoMode) return;
    const controller = new AbortController();
    let fetching = false;
    const load = async () => {
      if (fetching) return;
      fetching = true;
      try {
        const value = await api<Policy>('/auth/password-policy', { signal: controller.signal });
        if (!controller.signal.aborted) { setPolicy(value); setError(''); }
      } catch (cause) { if (!controller.signal.aborted) setError((cause as Error).message); }
      finally { fetching = false; }
    };
    void load();
    const timer = window.setInterval(load, 60000);
    window.addEventListener('focus', load);
    return () => { controller.abort(); clearInterval(timer); window.removeEventListener('focus', load); };
  }, [attempt]);
  async function skip() {
    if (!policy?.reminderDay || busy) return;
    setBusy(true); setError('');
    try {
      await api('/auth/password-reminder', { method: 'POST', body: JSON.stringify({ day: policy.reminderDay }) });
      setPolicy(null); setAttempt(value => value + 1);
    } catch (cause) { setError((cause as Error).message); }
    finally { setBusy(false); }
  }
  return <>
    {error && !policy?.reminderDay && <Alert type="warning" message="Chưa kiểm tra được hạn đổi mật khẩu" action={<Button onClick={() => setAttempt(value => value + 1)}>Thử lại</Button>} />}
    <Modal title="Nhắc đổi mật khẩu" open={!!policy?.reminderDay && !editing} closable={false} maskClosable={false} keyboard={false}
      footer={<Space><Button disabled={busy} onClick={onChange}>Đổi ngay</Button><Button loading={busy} onClick={() => void skip()}>Bỏ qua lần này</Button></Space>}>
      <p>{policy?.reminderDay === 1 ? 'Tài khoản mới được cấp hoặc đặt lại mật khẩu.' : `Đã đến ngày ${policy?.reminderDay} của chu kỳ đổi mật khẩu.`}</p>
      <p>Hãy đổi mật khẩu trước <strong>{policy?.deadline ? new Date(policy.deadline).toLocaleString('vi-VN') : 'hạn được thông báo'}</strong>. Hết hạn 7 ngày, tài khoản sẽ bị khóa; cần Admin mở lại.</p>
      <p>Bỏ qua không gia hạn. Hệ thống nhắc lại ở ngày 5, 6 và 7 nếu bạn chưa đổi.</p>
      {error && <Alert type="error" showIcon message={error} />}
    </Modal>
  </>;
}
