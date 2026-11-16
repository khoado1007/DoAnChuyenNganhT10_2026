import { useEffect, useState, type ReactNode } from 'react';
import { Alert, Button, Card, Descriptions, Input, Progress, Table, Tabs, Tag } from 'antd';
import { api } from '../api/auth';
import type { DeviceSession } from '../api/machines';
import { displayDate, displayValue, specificationLabels, type MachineInformation, type Software } from '../types/machine-information';

/** Thông tin mô phỏng chỉ đọc; API xác thực cả tài khoản lẫn phiên thiết bị. */
export default function MachineWorkspace({ session, children }: { session: DeviceSession; children: ReactNode }) {
  const [information, setInformation] = useState<MachineInformation | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const [search, setSearch] = useState('');
  const [refreshedAt, setRefreshedAt] = useState<string | null>(null);
  useEffect(() => {
    const controller = new AbortController(); setInformation(null); setRefreshedAt(null); setLoading(true); setError('');
    api<MachineInformation>(`/machines/${encodeURIComponent(session.deviceId)}/information`, { signal: controller.signal, headers: { 'X-Device-Session': session.sessionId } })
      .then(value => { if (!controller.signal.aborted) { setInformation(value); setRefreshedAt(new Date().toISOString()); } })
      .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Không tải được cấu hình máy.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [session.deviceId, session.sessionId, attempt]);
  const assetLabels = { assetCode: 'Mã tài sản', name: 'Tên máy', status: 'Trạng thái mô phỏng', serialNumber: 'Serial', manufacturer: 'Hãng sản xuất', model: 'Model', createdAt: 'Ngày tạo', updatedAt: 'Cập nhật gần nhất' };
  const statusLabels: Record<string, string> = { simulated: 'Mô phỏng', Available: 'Sẵn sàng', Assigned: 'Đã bàn giao', UnderMaintenance: 'Bảo trì', Retired: 'Ngừng sử dụng', Disabled: 'Vô hiệu hóa' };
  return <>
    <Card className="machine-summary" title="Máy đang sử dụng" extra={<Button loading={loading} onClick={() => setAttempt(value => value + 1)}>Làm mới thông tin</Button>} style={{ marginBottom: 16 }}>
      <strong>{session.deviceName}</strong> <Tag color="blue">Mô phỏng</Tag>
      <p>{session.domainId} · {displayValue(information?.assetCode)}</p>
      <p>Cấu hình do quản trị khai báo; không phản ánh trạng thái online của máy thật.</p>
      <div className="machine-summary-grid">
        <section className="machine-summary-metric"><span>Ổ đĩa còn trống / tổng</span><strong>{information?.diskFreeGb == null || information?.diskTotalGb == null ? 'Chưa khai báo' : `${information.diskFreeGb} / ${information.diskTotalGb} GB`}</strong>
          {information?.diskFreeGb != null && information?.diskTotalGb != null && information.diskTotalGb > 0 && <Progress percent={Math.round(information.diskFreeGb / information.diskTotalGb * 100)} showInfo={false} strokeColor="#286b94" aria-label="Tỷ lệ dung lượng ổ đĩa còn trống" />}
        </section>
        <section className="machine-summary-metric"><span>Bộ nhớ RAM</span><strong>{information?.memoryGb == null ? 'Chưa khai báo' : `${information.memoryGb} GB`}</strong></section>
        <section className="machine-summary-metric"><span>Bộ xử lý</span><strong>{displayValue(information?.processor)}</strong></section>
        <section className="machine-summary-metric"><span>Hệ điều hành</span><strong>{displayValue(information?.operatingSystem)}</strong></section>
        <section className="machine-summary-metric"><span>Mã hóa ổ đĩa</span><strong>{displayValue(information?.diskEncryption)}</strong></section>
        <section className="machine-summary-metric"><span>Làm mới gần nhất</span><strong>{displayDate(refreshedAt)}</strong></section>
      </div>
    </Card>
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setAttempt(value => value + 1)}>Thử lại</Button>} />}
    <Tabs defaultActiveKey="details" items={[
      { key: 'details', label: 'Chi tiết', children: <Card loading={loading}><Descriptions bordered column={{ xs: 1, sm: 1, md: 2 }} items={Object.entries({ ...assetLabels, ...specificationLabels }).map(([key, label]) => {
        const value = information?.[key as keyof MachineInformation];
        return { key, label, children: key.endsWith('At') ? displayDate(value) : key === 'status' ? statusLabels[String(value)] ?? displayValue(value) : displayValue(value) };
      })} /></Card> },
      { key: 'software', label: 'Phần mềm', children: <Card>
        <Input.Search aria-label="Tìm phần mềm" placeholder="Tên, phiên bản hoặc đường dẫn" allowClear value={search} onChange={event => setSearch(event.target.value)} style={{ maxWidth: 420, marginBottom: 16 }} />
        <Table<Software> loading={loading} rowKey={(_, index) => String(index)} scroll={{ x: 1000 }} pagination={{ pageSize: 10, showSizeChanger: false }} locale={{ emptyText: 'Chưa khai báo phần mềm' }} dataSource={(information?.software ?? []).filter(item => [item.name, item.version, item.filePath].some(value => value?.toLowerCase().includes(search.toLowerCase())))} columns={[
          { title: 'Tên phần mềm', dataIndex: 'name', render: displayValue }, { title: 'Phiên bản', dataIndex: 'version', render: displayValue },
          { title: 'Loại', dataIndex: 'type', render: displayValue }, { title: 'Lỗ hổng', dataIndex: 'vulnerabilities', render: displayValue },
          { title: 'Sử dụng gần nhất', dataIndex: 'lastUsedAt', render: displayDate },
          { title: 'Đường dẫn (chỉ hiển thị)', dataIndex: 'filePath', render: value => <span style={{ overflowWrap: 'anywhere' }}>{displayValue(value)}</span> },
        ]} />
      </Card> },
      { key: 'files', label: 'Desktop / Documents', children },
    ]} />
  </>;
}
