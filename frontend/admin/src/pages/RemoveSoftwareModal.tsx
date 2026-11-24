import { useEffect, useRef, useState } from 'react';
import { Alert, Button, Form, Input, Modal, Space } from 'antd';
import { api } from '../api/auth';
import { ApiError } from '../api/client';
import type { DeviceSession } from '../api/machines';
import { softwareKindLabel, type MachineInformation, type Software } from '../types/machine-information';

type Approval = { adminUsername: string; adminPassword: string };
export default function RemoveSoftwareModal({ session, item, revision, onClose, onRemoved, onReload }: {
  session: DeviceSession; item: Software; revision: string;
  onClose: () => void; onRemoved: (value: MachineInformation) => void; onReload: () => void;
}) {
  const [form] = Form.useForm<Approval>();
  const [busy, setBusy] = useState(false), [error, setError] = useState(''), [changed, setChanged] = useState(false);
  const active = useRef<AbortController | null>(null);
  useEffect(() => () => { active.current?.abort(); }, []);
  async function remove(values: Approval) {
    if (busy || changed || !item.id) return;
    setBusy(true); setError('');
    const controller = new AbortController(); active.current = controller;
    const body = JSON.stringify({ ...values, revision });
    form.setFieldValue('adminPassword', '');
    try {
      const result = await api<MachineInformation>(`/machines/${encodeURIComponent(session.deviceId)}/software/${encodeURIComponent(item.id)}/remove`, {
        method: 'POST', headers: { 'X-Device-Session': session.sessionId }, body, signal: controller.signal,
      });
      if (!controller.signal.aborted) onRemoved(result);
    } catch (cause) {
      if (!controller.signal.aborted) {
        setError(cause instanceof Error ? cause.message : 'Không thể xóa phần mềm.');
        setChanged(cause instanceof ApiError && [404, 409].includes(cause.status));
      }
    } finally { if (!controller.signal.aborted) setBusy(false); }
  }
  return <Modal title="Admin xác nhận xóa phần mềm" open footer={null} onCancel={() => { if (!busy) onClose(); }} closable={!busy} maskClosable={!busy} keyboard={!busy}>
    <p><strong>{item.name}</strong></p>
    <p>{softwareKindLabel(item.kind)} · {session.deviceName}</p>
    <p>Admin xác nhận để xóa mục này khỏi danh sách mô phỏng. Phiên nhân viên vẫn được giữ.</p>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Form form={form} layout="vertical" onFinish={remove} disabled={busy || changed} preserve={false}>
      <Form.Item name="adminUsername" label="Tài khoản Admin" rules={[{ required: true, whitespace: true, message: 'Nhập tài khoản Admin.' }]}>
        <Input maxLength={100} autoComplete="off" autoFocus />
      </Form.Item>
      <Form.Item name="adminPassword" label="Mật khẩu Admin" rules={[{ required: true, message: 'Nhập mật khẩu Admin.' }]}>
        <Input.Password maxLength={256} autoComplete="off" />
      </Form.Item>
      <Space wrap>
        <Button danger type="primary" htmlType="submit" loading={busy} disabled={changed}>Xác nhận và xóa</Button>
        <Button disabled={busy} onClick={onClose}>Hủy</Button>
      </Space>
    </Form>
    {changed && <Button style={{ marginTop: 12 }} onClick={onReload}>Đóng và tải lại</Button>}
  </Modal>;
}
