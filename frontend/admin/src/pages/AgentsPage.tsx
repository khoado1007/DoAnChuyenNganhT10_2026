import { useEffect, useState } from 'react';
import { Alert, Button, Card, Form, Input, Modal, Popconfirm, Select, Space, Table, Tag, Typography } from 'antd';
import { api } from '../api/auth';

type Asset = { id: string; assetCode: string; name: string; status: string };
type Agent = {
  id: string;
  assetId: string;
  instanceId: string;
  assetCode: string;
  assetStatus: string;
  mode: 'real' | 'simulated';
  policy: string;
  connectivity: string;
  lastSeenAt: string | null;
  hostname: string;
  os: string;
  cpu: string;
  ramBytes: number;
  agentVersion: string;
  revokedAt?: string | null;
  revokedReason?: string | null;
};
type Token = { token: string; expiresAt: string };

/** Theo dõi Agent, tạo token đăng ký và quản lý chính sách thiết bị. */
export default function AgentsPage() {
  const [agents, setAgents] = useState<Agent[]>([]);
  const [assets, setAssets] = useState<Asset[]>([]);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [token, setToken] = useState<Token | null>(null);
  const [recoveryTarget, setRecoveryTarget] = useState<Agent | null>(null);
  const [assetForm] = Form.useForm();
  const [tokenForm] = Form.useForm();
  const [recoveryForm] = Form.useForm();

  // Đồng bộ danh sách Agent và tài sản; bỏ qua phản hồi nếu request đã bị hủy.
  async function refresh(signal?: AbortSignal) {
    try {
      const [rows, inventory] = await Promise.all([api<Agent[]>('/agents', { signal }), api<Asset[]>('/assets', { signal })]);
      if (signal?.aborted) return;
      setAgents(rows); setAssets(inventory); setError('');
    } catch (cause) {
      if (!signal?.aborted) setError(cause instanceof Error ? cause.message : 'Không tải được danh sách Agent.');
    } finally { if (!signal?.aborted) setLoading(false); }
  }
  useEffect(() => {
    // Tải dữ liệu khi mở trang và làm mới định kỳ; dừng bộ hẹn giờ khi rời trang.
    const controller = new AbortController();
    void refresh(controller.signal);

    return () => controller.abort();
  }, []);

  // Chạy thao tác quản trị, tải lại dữ liệu khi thành công và báo lỗi nếu thất bại.
  async function mutate(action: () => Promise<void>) {
    setBusy(true); setError('');
    try { await action(); await refresh(); }
    catch (cause) { setError(cause instanceof Error ? cause.message : 'Thao tác thất bại.'); }
    finally { setBusy(false); }
  }

  const available = assets.filter(asset => !agents.some(agent => agent.assetId === asset.id && agent.policy !== 'revoked') && !['Disabled', 'Retired'].includes(asset.status));
  return <main className="page-content">
    <div className="page-heading"><div><Typography.Title level={1}>Agent</Typography.Title><Typography.Text>Đăng ký và phục hồi Agent</Typography.Text></div><Button loading={loading} onClick={() => void refresh()}>Làm mới</Button></div>
    {error && <Alert type="error" showIcon message={error} style={{ marginBottom: 16 }} />}
    <Alert type="info" showIcon message="Real thu thập thông tin máy khi được đồng ý; Simulated gửi dữ liệu giả lập. Hai Agent chạy trên một máy không đại diện cho hai máy vật lý." style={{ marginBottom: 16 }} />
    <Space direction="vertical" size={16} style={{ width: '100%' }}>
      <Card title="Chuẩn bị đăng ký">
        <Form form={assetForm} layout="inline" onFinish={values => void mutate(async () => { await api('/assets', { method: 'POST', body: JSON.stringify(values) }); assetForm.resetFields(); })}>
          <Form.Item name="assetCode" label="Mã tài sản" rules={[{ required: true, whitespace: true }]}><Input maxLength={50} placeholder="PC-001" /></Form.Item>
          <Form.Item name="name" label="Tên tài sản" rules={[{ required: true, whitespace: true }]}><Input maxLength={200} placeholder="Máy phòng IT" /></Form.Item>
          <Form.Item><Button htmlType="submit" loading={busy}>Tạo tài sản</Button></Form.Item>
        </Form>
        <Form form={tokenForm} layout="vertical" initialValues={{ mode: 'real' }} style={{ marginTop: 20, maxWidth: 560 }} onFinish={values => void mutate(async () => { setToken(await api<Token>('/agents/enrollment-tokens', { method: 'POST', body: JSON.stringify(values) })); })}>
          <Form.Item name="assetId" label="Tài sản chưa đăng ký Agent" rules={[{ required: true }]}><Select loading={loading} placeholder="Chọn tài sản" options={available.map(asset => ({ value: asset.id, label: `${asset.assetCode} · ${asset.name}` }))} /></Form.Item>
          <Form.Item name="mode" label="Chế độ" rules={[{ required: true }]}><Select options={[{ value: 'real', label: 'Real — máy thực' }, { value: 'simulated', label: 'Simulated — dữ liệu giả lập' }]} /></Form.Item>
          <Button type="primary" htmlType="submit" loading={busy} disabled={available.length === 0}>Cấp token đăng ký</Button>
          <Typography.Paragraph type="secondary" style={{ marginTop: 8 }}>Token có hiệu lực 15 phút, dùng một lần. Cấp token mới sẽ hủy token cũ chưa dùng của tài sản.</Typography.Paragraph>
        </Form>
      </Card>
      <Card title="Agent đã đăng ký">
        <Table<Agent> rowKey="id" loading={loading} dataSource={agents} scroll={{ x: 1200 }} locale={{ emptyText: 'Chưa có Agent đăng ký' }} pagination={{ pageSize: 10 }} columns={[
          { title: 'Tài sản / Agent', key: 'identity', render: (_: unknown, row: Agent) => <><b>{row.assetCode}</b><div>{row.hostname || 'Chưa có tên máy'}</div><Typography.Text type="secondary">{row.instanceId}</Typography.Text></> },
          { title: 'Chế độ', dataIndex: 'mode', render: (value: string) => <Tag color={value === 'simulated' ? 'orange' : 'blue'}>{value === 'simulated' ? 'Simulated' : 'Real'}</Tag> },
          {
            title: 'Chính sách',
            dataIndex: 'policy',
            render: (value: string, row: Agent) => (
              <>
                <Tag color={value === 'revoked' ? 'magenta' : (value === 'active' ? 'blue' : 'default')}>
                  {value === 'revoked' ? 'Đã thu hồi' : (value === 'active' ? 'Hoạt động' : 'Đã vô hiệu hóa')}
                </Tag>
                {row.revokedReason && <div style={{ fontSize: 12, color: '#8c8c8c', marginTop: 2 }}>{row.revokedReason}</div>}
              </>
            ),
          },
          { title: 'Vòng đời tài sản', dataIndex: 'assetStatus' },
          { title: 'Inventory', key: 'inventory', render: (_: unknown, row: Agent) => <>{row.os}<div>{row.cpu}</div>{row.ramBytes > 0 && <div>{(row.ramBytes / 1024 ** 3).toFixed(1)} GB RAM · v{row.agentVersion}</div>}</> },
          {
            title: 'Thao tác',
            key: 'actions',
            render: (_: unknown, row: Agent) => (
              <Space size="small">
                <Popconfirm
                  title="Vô hiệu hóa Agent?"
                  description="Máy chủ sẽ từ chối yêu cầu tiếp theo. Lịch sử vẫn được giữ."
                  onConfirm={() => mutate(async () => { await api(`/agents/${row.id}/disable`, { method: 'POST' }); })}
                  disabled={row.policy !== 'active' || busy}
                >
                  <Button danger size="small" disabled={row.policy !== 'active' || busy}>Vô hiệu hóa</Button>
                </Popconfirm>
                {row.policy !== 'revoked' && (
                  <Button
                    size="small"
                    disabled={busy}
                    onClick={() => {
                      setRecoveryTarget(row);
                      recoveryForm.setFieldsValue({ mode: row.mode, reason: 'Cài lại máy hoặc mất cấu hình' });
                    }}
                  >
                    Cấp lại / Phục hồi
                  </Button>
                )}
              </Space>
            ),
          },
        ]} />
      </Card>
    </Space>
    <Modal
      title={`Cấp lại token phục hồi cho tài sản: ${recoveryTarget?.assetCode}`}
      open={recoveryTarget !== null}
      onCancel={() => setRecoveryTarget(null)}
      footer={null}
      destroyOnClose
    >
      {recoveryTarget && (
        <Form
          form={recoveryForm}
          layout="vertical"
          initialValues={{ mode: recoveryTarget.mode, reason: 'Cài lại máy hoặc mất cấu hình' }}
          onFinish={values => void mutate(async () => {
            const issued = await api<Token>('/agents/recovery-tokens', {
              method: 'POST',
              body: JSON.stringify({
                assetId: recoveryTarget.assetId,
                mode: values.mode,
                reason: values.reason,
              }),
            });
            setRecoveryTarget(null);
            setToken(issued);
          })}
        >
          <Alert
            type="warning"
            showIcon
            message="Thao tác này sẽ thu hồi vĩnh viễn Agent hiện tại của tài sản này trên máy chủ. Credential cũ sẽ bị từ chối ngay lập tức."
            style={{ marginBottom: 16 }}
          />
          <Form.Item name="mode" label="Chế độ Agent" rules={[{ required: true }]}>
            <Select options={[
              { value: 'real', label: 'Real — máy thực' },
              { value: 'simulated', label: 'Simulated — dữ liệu giả lập' },
            ]} />
          </Form.Item>
          <Form.Item name="reason" label="Lý do cấp lại / phục hồi" rules={[{ required: true, whitespace: true }]}>
            <Input maxLength={200} placeholder="Ví dụ: Cài lại máy, mất state.json" />
          </Form.Item>
          <Space style={{ display: 'flex', justifyContent: 'flex-end', marginTop: 16 }}>
            <Button onClick={() => setRecoveryTarget(null)}>Hủy</Button>
            <Button type="primary" htmlType="submit" loading={busy}>
              Xác nhận cấp lại token
            </Button>
          </Space>
        </Form>
      )}
    </Modal>
    <Modal title="Token đăng ký — chỉ hiển thị lần này" open={token !== null} onCancel={() => setToken(null)} footer={<Button onClick={() => setToken(null)}>Đã lưu, đóng</Button>} destroyOnClose>
      {token && <><Typography.Paragraph>Nhập token vào lời nhắc đăng ký của Python Agent. Không gửi token qua chat hay lưu vào source code.</Typography.Paragraph><Input.TextArea aria-label="Token đăng ký" readOnly value={token.token} autoSize /><Typography.Paragraph style={{ marginTop: 8 }}>Hướng dẫn phục hồi: Nếu máy đã có file state cũ, chạy:<pre style={{ background: '#f5f5f5', padding: '6px 8px', borderRadius: 4, marginTop: 4, overflowX: 'auto' }}>python itam_agent.py re-enroll --server ... --mode ... --state ...</pre>hoặc chạy lệnh <code>enroll</code> nếu khởi tạo trên máy mới/state sạch.</Typography.Paragraph><Typography.Paragraph type="secondary">Hết hạn: {new Date(token.expiresAt).toLocaleString('vi-VN')}</Typography.Paragraph></>}
    </Modal>
  </main>;
}
