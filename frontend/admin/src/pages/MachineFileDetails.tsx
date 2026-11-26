import { useEffect, useState } from 'react';
import { Alert, Button, Card, Table } from 'antd';
import { api } from '../api/auth';
import type { DeviceSession } from '../api/machines';
import type { PagedResult } from '../types/api';

type FileInfo = { name: string; extension: string; folder: string; sizeBytes: number; owner: string };
type FileEvent = { fileName: string | null; kind: string; completedAt: string; owner: string };
type Details = { files: PagedResult<FileInfo>; history: PagedResult<FileEvent> };
const actions: Record<string, string> = {
  create: 'Tạo file', write: 'Cập nhật', rename: 'Đổi tên', move: 'Di chuyển',
  copy: 'Sao chép', delete: 'Chuyển vào thùng rác', restore: 'Khôi phục',
};

export default function MachineFileDetails({ session, revision }: { session: DeviceSession; revision: number }) {
  const [filesPage, setFilesPage] = useState(1);
  const [historyPage, setHistoryPage] = useState(1);
  const [details, setDetails] = useState<Details | null>(null);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(true);
  const [retry, setRetry] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError('');
    api<Details>(`/machines/${session.deviceId}/file-details?filesPage=${filesPage}&historyPage=${historyPage}`, {
      signal: controller.signal, headers: { 'X-Device-Session': session.sessionId },
    }).then(data => {
      if (!controller.signal.aborted) setDetails(data);
    }).catch(cause => {
      if (!controller.signal.aborted) { setDetails(null); setError(cause.message); }
    }).finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [session.deviceId, session.sessionId, filesPage, historyPage, revision, retry]);

  if (error) return <Alert type="error" showIcon message={error} action={<Button onClick={() => setRetry(r => r + 1)}>Thử lại</Button>} />;
  return <>
    <Card title="File trên máy" style={{ marginTop: 16 }}>
      <Table size="small" loading={loading} dataSource={details?.files.items.map((f, i) => ({ ...f, key: `${details.files.page}-${i}` }))} scroll={{ x: 640 }}
        locale={{ emptyText: 'Máy chưa có file' }}
        pagination={{ current: details?.files.page ?? filesPage, total: details?.files.totalCount, pageSize: 10, showSizeChanger: false, onChange: setFilesPage }}
        columns={[
          { title: 'Tên file', dataIndex: 'name' }, { title: 'Loại', dataIndex: 'extension' },
          { title: 'Thư mục', dataIndex: 'folder', render: v => v === 'RecycleBin' ? 'Thùng rác' : v },
          { title: 'Dung lượng (byte)', dataIndex: 'sizeBytes' }, { title: 'Chủ sở hữu', dataIndex: 'owner' },
        ]} />
    </Card>
    <Card title="Lịch sử file trên máy" style={{ marginTop: 16 }}>
      <Table size="small" loading={loading} dataSource={details?.history.items.map((e, i) => ({ ...e, key: `${details.history.page}-${i}` }))} scroll={{ x: 640 }}
        locale={{ emptyText: 'Chưa có thay đổi file' }}
        pagination={{ current: details?.history.page ?? historyPage, total: details?.history.totalCount, pageSize: 10, showSizeChanger: false, onChange: setHistoryPage }}
        columns={[
          { title: 'Thời gian', dataIndex: 'completedAt', render: (v: string) => new Date(v.endsWith('Z') ? v : v + 'Z').toLocaleString('vi-VN') },
          { title: 'Người thực hiện', dataIndex: 'owner' }, { title: 'Thao tác', dataIndex: 'kind', render: v => actions[v] ?? v },
          { title: 'Tên file', dataIndex: 'fileName', render: v => v ?? 'Chưa ghi tên' },
        ]} />
    </Card>
  </>;
}
