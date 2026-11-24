import {useEffect, useState} from 'react';
import {Alert, Button, Select, Space, Table, Tag, Tooltip, Typography} from 'antd';
import {SearchOutlined, ReloadOutlined} from '@ant-design/icons';
import {api} from '../api/auth';
import {isDemoMode} from '../demo/mode';
import type {ColumnsType} from 'antd/es/table';
import type {PagedResult} from '../types/api';
import {useAdminFilterOptions} from './useAdminFilterOptions';

const {Text} = Typography;

type AdminFile = {
  id: string;
  profileId: string;
  userId: string;
  userName: string | null;
  assetId: string;
  assetName: string | null;
  folder: string;
  name: string;
  extension: string;
  sizeBytes: number;
  storageBackend: string;
};


const DEMO_FILES: AdminFile[] = [
  {id:'1',profileId:'p1',userId:'u1',userName:'Nguyễn Văn A',assetId:'a1',assetName:'PC-001',folder:'Desktop',name:'bao_cao.docx',extension:'.docx',sizeBytes:24576,storageBackend:'Server'},
  {id:'2',profileId:'p1',userId:'u1',userName:'Nguyễn Văn A',assetId:'a1',assetName:'PC-001',folder:'Documents',name:'data.csv',extension:'.csv',sizeBytes:1024,storageBackend:'Server'},
  {id:'3',profileId:'p2',userId:'u2',userName:'Trần Thị B',assetId:'a2',assetName:'LAPTOP-002',folder:'Desktop',name:'notes.txt',extension:'.txt',sizeBytes:512,storageBackend:'Server'},
];

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1048576) return `${(bytes/1024).toFixed(1)} KB`;
  return `${(bytes/1048576).toFixed(1)} MB`;
}

export default function FilesAdminPage() {
  const [rows, setRows] = useState<AdminFile[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const filters = useAdminFilterOptions(true);
  const {users, assets} = filters;
  const [applied, setApplied] = useState({user: '', asset: '', folder: ''});
  const [userFilter, setUserFilter] = useState('');
  const [assetFilter, setAssetFilter] = useState('');
  const [folderFilter, setFolderFilter] = useState('');
  const [revision, setRevision] = useState(0);

  const pageSize = 50;

  useEffect(() => {
    if (isDemoMode) {
      const filtered = DEMO_FILES.filter(row => !applied.folder || row.folder === applied.folder);
      setRows(filtered); setTotal(filtered.length);
      return;
    }
    const c = new AbortController();
    setLoading(true); setRows([]); setTotal(0);
    setError('');
    const params = new URLSearchParams();
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    if (applied.user) params.set('userId', applied.user);
    if (applied.asset) params.set('assetId', applied.asset);
    if (applied.folder) params.set('folder', applied.folder);

    api<PagedResult<AdminFile>>(`/admin/files?${params}`, {signal: c.signal})
      .then(d => {
        if (!c.signal.aborted) {
          setRows(d.items);
          setTotal(d.totalCount);
        }
      })
      .catch(e => { if (!c.signal.aborted) setError(e.message); })
      .finally(() => { if (!c.signal.aborted) setLoading(false); });
    return () => c.abort();
  }, [page, revision, applied]);

  function search() {
    setApplied({user: userFilter, asset: assetFilter, folder: folderFilter});
    setPage(1);
    setRevision(v => v + 1);
  }

  const columns: ColumnsType<AdminFile> = [
    {title: 'Loại', dataIndex: 'extension', width: 90},
    {
      title: 'Tên file',
      dataIndex: 'name',
      ellipsis: true,
      render: (v, row) => (
        <Tooltip title={`${row.folder}/${v}`}>
          <Text>{v}</Text>
        </Tooltip>
      ),
    },
    {
      title: 'Thư mục',
      dataIndex: 'folder',
      width: 120,
      render: v => <Tag color={v==='Desktop'?'blue':v==='Documents'?'cyan':'default'}>{v}</Tag>,
    },
    {
      title: 'Dung lượng',
      dataIndex: 'sizeBytes',
      width: 100,
      align: 'right',
      render: v => <Text style={{fontSize:13}}>{formatBytes(v)}</Text>,
    },
    {
      title: 'Tài khoản',
      dataIndex: 'userName',
      width: 180,
      render: (v, row) => v ?? <Text type="secondary">{row.userId.slice(0,8)}…</Text>,
    },
    {
      title: 'Máy',
      dataIndex: 'assetName',
      width: 160,
      render: (v, row) => v ?? <Text type="secondary">{row.assetId.slice(0,8)}…</Text>,
    },
    {
      title: 'Kho',
      dataIndex: 'storageBackend',
      width: 90,
      render: v => <Tag color={v==='Server'?'geekblue':'purple'}>{v}</Tag>,
    },
  ];

  return (
    <div className="page-content">
      <Typography.Paragraph>Thông tin file hiện tại · chỉ đọc metadata, không mở nội dung cá nhân.</Typography.Paragraph>
      {filters.error && <Alert type="warning" showIcon message={filters.error} action={<Button onClick={filters.retry}>Tải lại bộ lọc</Button>} style={{marginBottom:12}} />}
      <div style={{marginBottom:16, display:'flex', gap:8, flexWrap:'wrap', alignItems:'center'}}>
        {!isDemoMode && (
          <>
            <Select
              allowClear
              showSearch
              aria-label="Lọc theo tài khoản" loading={filters.loading}
              placeholder="Lọc theo tài khoản"
              style={{width: 220}}
              value={userFilter || undefined}
              onChange={v => setUserFilter(v ?? '')}
              options={users.map(u => ({value: u.id, label: u.fullName || u.username}))}
              optionFilterProp="label"
            />
            <Select
              allowClear
              showSearch
              aria-label="Lọc theo máy" loading={filters.loading}
              placeholder="Lọc theo máy"
              style={{width: 180}}
              value={assetFilter || undefined}
              onChange={v => setAssetFilter(v ?? '')}
              options={assets.map(a => ({value: a.id, label: a.name}))}
              optionFilterProp="label"
            />
          </>
        )}
        <Select
          allowClear
          aria-label="Thư mục"
          placeholder="Thư mục"
          style={{width: 140}}
          value={folderFilter || undefined}
          onChange={v => setFolderFilter(v ?? '')}
          options={[
            {value:'Desktop', label:'Desktop'},
            {value:'Documents', label:'Documents'},
            {value:'RecycleBin', label:'Thùng rác'},
          ]}
        />
        <Space>
          <Button icon={<SearchOutlined />} type="primary" aria-label="Lọc" onClick={search}>Lọc</Button>
          <Button icon={<ReloadOutlined />} aria-label="Đặt lại" onClick={() => {
            setUserFilter(''); setAssetFilter(''); setFolderFilter(''); setApplied({user:'',asset:'',folder:''});
            setPage(1); setRevision(v=>v+1);
          }}>Đặt lại</Button>
        </Space>
      </div>
      {error && <Alert type="error" message={error} action={<Button onClick={() => setRevision(v=>v+1)}>Thử lại</Button>} style={{marginBottom:12}} />}
      <Table<AdminFile>
        rowKey="id"
        loading={loading}
        dataSource={rows}
        columns={columns}
        pagination={{
          current: page,
          pageSize,
          total,
          showTotal: t => `${t} file`,
          onChange: p => setPage(p),
          showSizeChanger: false,
        }}
        size="middle"
        scroll={{x: 800}}
        locale={{emptyText: 'Chưa có file nào.'}}
      />
      <div style={{marginTop:8}}>
        <Text type="secondary" style={{fontSize:12}}>
          * Chỉ hiển thị tên, thư mục, dung lượng, chủ sở hữu và máy. Admin không xem được nội dung file.
        </Text>
      </div>
    </div>
  );
}
