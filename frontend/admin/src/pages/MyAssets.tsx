import { useEffect, useState } from 'react';
import { Alert, Button, Card, Table } from 'antd';
import { api } from '../api/auth';
import type { AssignmentResponse } from '../types/assignment';
/** Hiển thị danh sách tài sản đang được cấp cho người dùng hiện tại. */
export default function MyAssets() {
  const [rows, setRows] = useState<AssignmentResponse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    // Tải lại danh sách theo phiên bản làm mới và hủy request khi component bị tháo.
    const abort = new AbortController(); setLoading(true); setError('');
    api<AssignmentResponse[]>('/me/assets', { signal: abort.signal })
      .then(data => { if (!abort.signal.aborted) setRows(data); })
      .catch(e => { if (!abort.signal.aborted) { setRows([]); setError(e.message); } })
      .finally(() => { if (!abort.signal.aborted) setLoading(false); });
    return () => abort.abort();
  }, [revision]);
  return <Card title="Tài sản của tôi" style={{ marginTop: 16, minWidth: 0 }} extra={<Button onClick={() => setRevision(r => r + 1)}>Làm mới</Button>}>
    {error && <Alert type="error" showIcon message={error} />}
    <Table<AssignmentResponse> rowKey="id" dataSource={rows} loading={loading} scroll={{ x: 500 }} columns={[
      { title: 'Mã tài sản', dataIndex: 'assetCode' }, { title: 'Thiết bị', dataIndex: 'assetName' },
      { title: 'Ngày nhận', dataIndex: 'assignedAt', render: (value: string) => new Date(value).toLocaleString('vi-VN') },
    ]} locale={{ emptyText: 'Bạn chưa được cấp tài sản.' }} />
  </Card>;
}

