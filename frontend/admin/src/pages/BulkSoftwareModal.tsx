import { useEffect, useState } from 'react';
import { Alert, Button, Card, Form, Input, Modal, Select, Space, Table } from 'antd';
import { api } from '../api/auth';
import type { AssetResponse } from '../types/asset';

type SoftwareInput = { name: string; kind: 'App' | 'Service'; version?: string; filePath?: string };
type Values = { assetIds: string[]; software: SoftwareInput[] };
type MachineResult = { assetId: string; assetName: string; added: number; skipped: number };
type BulkResult = { machines: MachineResult[]; added: number; skipped: number };

/** Adds the same declared software list to selected machines; existing entries are kept. */
export default function BulkSoftwareModal({ onClose }: { onClose: () => void }) {
  const [form] = Form.useForm<Values>();
  const [assets, setAssets] = useState<AssetResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState('');
  const [result, setResult] = useState<BulkResult | null>(null);
  const assetIds = Form.useWatch('assetIds', form) as string[] | undefined;
  const software = Form.useWatch('software', form) as SoftwareInput[] | undefined;

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setLoadError('');
    api<AssetResponse[]>('/assets', { signal: controller.signal })
      .then(value => { if (!controller.signal.aborted) setAssets(value); })
      .catch(cause => { if (!controller.signal.aborted) setLoadError(cause instanceof Error ? cause.message : 'Không tải được danh sách máy.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [attempt]);

  async function save(values: Values) {
    setSaving(true); setSaveError('');
    const optional = (value?: string) => value?.trim() || null;
    try {
      const data = await api<BulkResult>('/assets/software/bulk', { method: 'POST', body: JSON.stringify({
        assetIds: values.assetIds,
        software: values.software.map(item => ({ name: item.name.trim(), kind: item.kind, version: optional(item.version), filePath: optional(item.filePath) })),
      }) });
      setResult(data);
    } catch (cause) { setSaveError(cause instanceof Error ? cause.message : 'Không thêm được phần mềm. Vui lòng thử lại.'); }
    finally { setSaving(false); }
  }

  return <Modal title="Thêm phần mềm hàng loạt" open width={820} footer={null} closable={!saving} maskClosable={!saving} keyboard={!saving} onCancel={() => { if (!saving) onClose(); }}>
    <p>Khai báo ứng dụng hoặc dịch vụ cho một hay nhiều máy mô phỏng. Không tính quota và không ghi nhật ký file.</p>
    {result ? <>
      <Alert type="success" showIcon message={`Đã thêm ${result.added} mục; bỏ qua ${result.skipped} mục trùng.`} style={{ marginBottom: 16 }} />
      <Table<MachineResult> rowKey="assetId" size="small" dataSource={result.machines} pagination={false} scroll={{ x: 400 }} columns={[
        { title: 'Máy', dataIndex: 'assetName' },
        { title: 'Đã thêm', dataIndex: 'added', width: 90 },
        { title: 'Bỏ qua', dataIndex: 'skipped', width: 90 },
      ]} />
      <Button type="primary" onClick={onClose} style={{ marginTop: 16 }}>Đóng</Button>
    </> : <>
      <Alert type="info" showIcon message="Mục cùng tên và phân loại sẽ được bỏ qua; giữ nguyên bản đang có trên từng máy." style={{ marginBottom: 16 }} />
      {loading && <p role="status">Đang tải danh sách máy…</p>}
      {loadError && <Alert type="error" showIcon message={loadError} action={<Button onClick={() => setAttempt(value => value + 1)}>Tải lại danh sách máy</Button>} />}
      {!loading && !loadError && assets.length === 0 && <Alert type="info" showIcon message="Chưa có thiết bị. Thêm tài sản trước khi khai báo phần mềm." />}
      {!loading && !loadError && assets.length > 0 && <Form form={form} layout="vertical" onFinish={save} disabled={saving} initialValues={{ assetIds: [], software: [{}] }}>
        {saveError && <Alert type="error" showIcon message={saveError} style={{ marginBottom: 16 }} />}
        <Form.Item name="assetIds" label="Máy áp dụng" extra="Chọn tối đa 50 máy. Cùng danh sách bên dưới sẽ được thêm vào mỗi máy." rules={[
          { required: true, type: 'array', min: 1, message: 'Chọn ít nhất một máy.' },
          { type: 'array', max: 50, message: 'Chọn tối đa 50 máy mỗi lần.' },
        ]}>
          <Select mode="multiple" showSearch optionFilterProp="label" maxTagCount="responsive" maxCount={50} placeholder="Tìm theo mã hoặc tên máy" options={assets.map(asset => ({ value: asset.id, label: `${asset.assetCode} · ${asset.name}` }))} />
        </Form.Item>
        <Form.List name="software" rules={[{ validator: async (_, items) => { if (!items?.length || items.length > 200) throw new Error('Nhập từ 1 đến 200 phần mềm mỗi lần.'); } }]}>
          {(fields, { add, remove }, { errors }) => <>
            <h3>Danh sách phần mềm ({fields.length}/200)</h3>
            {fields.map(field => <Card key={field.key} size="small" style={{ marginBottom: 12 }} title={`Mục ${field.name + 1}`} extra={<Button danger disabled={saving || fields.length <= 1} onClick={() => remove(field.name)}>Xóa mục</Button>}>
              <Form.Item name={[field.name, 'name']} label="Tên phần mềm" rules={[{ required: true, whitespace: true, message: 'Nhập tên phần mềm.' }]}><Input maxLength={200} /></Form.Item>
              <Form.Item name={[field.name, 'kind']} label="Phân loại" rules={[{ required: true, message: 'Chọn App hoặc Service.' }]}><Select options={[{ value: 'App', label: 'Ứng dụng (App)' }, { value: 'Service', label: 'Dịch vụ (Service)' }]} /></Form.Item>
              <Form.Item name={[field.name, 'version']} label="Phiên bản"><Input maxLength={100} /></Form.Item>
              <Form.Item name={[field.name, 'filePath']} label="Đường dẫn (Admin)" extra="Nhân viên không nhìn thấy đường dẫn này."><Input maxLength={1000} /></Form.Item>
            </Card>)}
            <Form.ErrorList errors={errors} />
            <Button disabled={saving || fields.length >= 200} onClick={() => add({})}>Thêm mục</Button>
          </>}
        </Form.List>
        <p>{software?.length ?? 1} mục áp dụng cho {assetIds?.length ?? 0} máy.</p>
        <Space wrap><Button type="primary" htmlType="submit" loading={saving}>Áp dụng danh sách</Button><Button disabled={saving} onClick={onClose}>Hủy</Button></Space>
      </Form>}
    </>}
  </Modal>;
}
