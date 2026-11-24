import { useEffect, useState, type ReactNode } from 'react';
import { Alert, Button, Card, Descriptions, Input, Progress, Table, Tabs, Tag } from 'antd';
import { api } from '../api/auth';
import type { DeviceSession } from '../api/machines';
import { displayDate, displayValue, specificationLabels, softwareKindLabel, type MachineInformation, type Software } from '../types/machine-information';
import RemoveSoftwareModal from './RemoveSoftwareModal';

/** Employee xem cấu hình; mọi yêu cầu xóa phần mềm cần Admin xác nhận ở API. */
export default function MachineWorkspace({ session, children }: { session: DeviceSession; children: ReactNode }) {
  const [information, setInformation] = useState<MachineInformation | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [attempt, setAttempt] = useState(0);
  const [search, setSearch] = useState('');
  const [refreshedAt, setRefreshedAt] = useState<string | null>(null);
  const [removing, setRemoving] = useState<{ item: Software; revision: string } | null>(null);
  useEffect(() => {
    const controller = new AbortController(); setRemoving(null); setInformation(null); setRefreshedAt(null); setLoading(true); setError('');
    api<MachineInformation>(`/machines/${encodeURIComponent(session.deviceId)}/information`, { signal: controller.signal, headers: { 'X-Device-Session': session.sessionId } })
      .then(value => { if (!controller.signal.aborted) { setInformation(value); setRefreshedAt(new Date().toISOString()); } })
      .catch(cause => { if (!controller.signal.aborted) setError(cause instanceof Error ? cause.message : 'Không tải được cấu hình máy.'); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [session.deviceId, session.sessionId, attempt]);
  const assetLabels = { assetCode: 'Mã tài sản', name: 'Tên máy', status: 'Trạng thái mô phỏng', serialNumber: 'Serial', manufacturer: 'Hãng sản xuất', model: 'Model', createdAt: 'Ngày tạo', updatedAt: 'Cập nhật gần nhất' };
  const statusLabels: Record<string, string> = { simulated: 'Mô phỏng', Available: 'Sẵn sàng', Assigned: 'Đã bàn giao', UnderMaintenance: 'Bảo trì', Retired: 'Ngừng sử dụng', Disabled: 'Vô hiệu hóa' };
  return <>
    <Card className="machine-summary" title="Máy đang sử dụng" extra={<Button loading={loading} disabled={!!removing} onClick={() => setAttempt(value => value + 1)}>Làm mới thông tin</Button>} style={{ marginBottom: 16 }}>
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
        <p>Danh sách do Admin quản lý · Không tính quota tài liệu. Muốn xóa, cần Admin xác nhận trên máy này.</p>
        <Input.Search aria-label="Tìm phần mềm" placeholder="Tên, phiên bản hoặc loại phần mềm" allowClear value={search} onChange={event => setSearch(event.target.value)} style={{ maxWidth: 420, marginBottom: 16 }} />
        <Table<Software> loading={loading} rowKey={(item, index) => item.id ?? String(index)} scroll={{ x: 960 }} pagination={{ pageSize: 10, showSizeChanger: false }} locale={{ emptyText: 'Chưa khai báo phần mềm' }} dataSource={(information?.software ?? []).filter(item => [item.name, item.version, item.type, softwareKindLabel(item.kind)].some(value => value?.toLowerCase().includes(search.toLowerCase())))} columns={[
          { title: 'Tên phần mềm', dataIndex: 'name', render: displayValue }, { title: 'Phiên bản', dataIndex: 'version', render: displayValue },
          { title: 'Phân loại', dataIndex: 'kind', render: softwareKindLabel },
          { title: 'Loại chi tiết', dataIndex: 'type', render: displayValue }, { title: 'Lỗ hổng', dataIndex: 'vulnerabilities', render: displayValue },
          { title: 'Sử dụng gần nhất', dataIndex: 'lastUsedAt', render: displayDate },
          { title: 'Thao tác', key: 'actions', render: (_, item) => <Button disabled={!item.id || !information?.revision || loading} onClick={() => setRemoving({ item, revision: information!.revision! })}>Yêu cầu xóa</Button> },
        ]} />
      </Card> },
      { key: 'files', label: 'Desktop / Documents', children },
    ]} />
    {removing && <RemoveSoftwareModal key={`${session.sessionId}:${removing.item.id}`} session={session} item={removing.item} revision={removing.revision} onClose={() => setRemoving(null)} onReload={() => { setRemoving(null); setAttempt(value => value + 1); }} onRemoved={value => { setInformation(value); setRefreshedAt(new Date().toISOString()); setRemoving(null); }} />}
  </>;
}
