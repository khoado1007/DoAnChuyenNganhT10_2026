import { useEffect, useState } from 'react';
import { Alert, Button, Card, Form, Input, InputNumber, Modal, Select, Space } from 'antd';
import { api } from '../api/auth';
import { specificationLabels, type MachineSpecification, type Software } from '../types/machine-information';

type FormValues = Omit<MachineSpecification, 'lastRestartedAt' | 'software'> & {
  lastRestartedAt?: string; software: (Omit<Software, 'lastUsedAt'> & { lastUsedAt?: string })[];
};
const localDate = (value: string | null) => {
  if (!value) return undefined;
  const date = new Date(value);
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16);
};
export default function SpecificationModal({ assetId, onClose }: { assetId: string; onClose: () => void }) {
  const [form] = Form.useForm<FormValues>();
  const [loading, setLoading] = useState(true), [saving, setSaving] = useState(false), [error, setError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [loaded, setLoaded] = useState(false);
  useEffect(() => {
    const controller = new AbortController(); setLoading(true); setLoaded(false); setError('');
    api<MachineSpecification>(`/assets/${assetId}/specification`, { signal: controller.signal }).then(value => {
      if (!controller.signal.aborted) { form.setFieldsValue({ ...value, lastRestartedAt: localDate(value.lastRestartedAt), software: (value.software ?? []).map(item => ({ ...item, lastUsedAt: localDate(item.lastUsedAt) })) }); setLoaded(true); }
    }).catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [assetId, attempt, form]);
  async function save(values: FormValues) {
    setSaving(true); setError('');
    try {
      const body = Object.fromEntries(Object.keys(specificationLabels).map(key => [key, values[key as keyof FormValues] ?? null]));
      await api(`/assets/${assetId}/specification`, { method: 'PUT', body: JSON.stringify({ ...body, lastRestartedAt: values.lastRestartedAt ? new Date(values.lastRestartedAt).toISOString() : null, software: (values.software ?? []).map(item => ({ ...item, lastUsedAt: item.lastUsedAt ? new Date(item.lastUsedAt).toISOString() : null })) }) });
      onClose();
    } catch (cause) { setError((cause as Error).message); } finally { setSaving(false); }
  }
  return <Modal title="Cấu hình mô phỏng" open width={800} footer={null} onCancel={() => { if (!saving) onClose(); }}>
    <p>Dữ liệu khai báo cho máy mô phỏng. Không phải số liệu thu thập từ máy thật. Để trống nếu chưa biết.</p>
    {error && <Alert type="error" showIcon message={error} action={<Button disabled={saving} onClick={() => setAttempt(value => value + 1)}>Tải lại</Button>} />}
    {loading ? <p role="status">Đang tải cấu hình…</p> : loaded && <Form form={form} layout="vertical" onFinish={save} disabled={saving}>
      {Object.entries(specificationLabels).map(([key, label]) => <Form.Item key={key} name={key} label={label} rules={key === 'diskFreeGb' ? [{ validator: async (_, value) => { const total = form.getFieldValue('diskTotalGb'); if (value != null && (total == null || value > total)) throw new Error('Dung lượng trống cần có tổng ổ đĩa và không được lớn hơn tổng.'); } }] : []} dependencies={key === 'diskFreeGb' ? ['diskTotalGb'] : undefined}>
        {key.endsWith('Gb') ? <InputNumber min={0} style={{ width: '100%' }} /> : key === 'diskEncryption' ? <Select allowClear placeholder="Chưa khai báo" options={[{ value: true, label: 'Có' }, { value: false, label: 'Không' }]} /> : key.endsWith('At') ? <Input type="datetime-local" /> : <Input maxLength={key.endsWith('Ip') ? 45 : 200} />}
      </Form.Item>)}
      <Form.List name="software">{(fields, { add, remove }) => <>
        <h3>Phần mềm ({fields.length}/200)</h3>
        {fields.map(field => <Card key={field.key} size="small" style={{ marginBottom: 12 }} title={`Phần mềm ${field.name + 1}`} extra={<Button danger disabled={saving} onClick={() => remove(field.name)}>Xóa dòng</Button>}>
          <Form.Item name={[field.name, 'name']} label="Tên phần mềm" rules={[{ required: true, whitespace: true }]}><Input maxLength={200} /></Form.Item>
          <Form.Item name={[field.name, 'version']} label="Phiên bản"><Input maxLength={100} /></Form.Item>
          <Form.Item name={[field.name, 'type']} label="Loại"><Input maxLength={100} /></Form.Item>
          <Form.Item name={[field.name, 'vulnerabilities']} label="Lỗ hổng"><Input.TextArea maxLength={2000} /></Form.Item>
          <Form.Item name={[field.name, 'lastUsedAt']} label="Sử dụng gần nhất"><Input type="datetime-local" /></Form.Item>
          <Form.Item name={[field.name, 'filePath']} label="Đường dẫn (chỉ hiển thị)"><Input maxLength={1000} /></Form.Item>
        </Card>)}
        <Button disabled={saving || fields.length >= 200} onClick={() => add({})}>Thêm phần mềm</Button>
      </>}</Form.List>
      <Space style={{ marginTop: 20 }}><Button type="primary" htmlType="submit" loading={saving}>Lưu cấu hình</Button><Button disabled={saving} onClick={onClose}>Hủy</Button></Space>
    </Form>}
  </Modal>;
}
