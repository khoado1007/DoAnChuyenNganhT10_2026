import {useEffect, useState} from 'react';
import {Alert, Button, DatePicker, Select, Space, Table, Tag, Tooltip, Typography} from 'antd';
import {SearchOutlined, ReloadOutlined} from '@ant-design/icons';
import {api} from '../api/auth';
import {isDemoMode} from '../demo/mode';
import type {ColumnsType} from 'antd/es/table';
import type {PagedResult} from '../types/api';
import dayjs from 'dayjs';
import {useAdminFilterOptions} from './useAdminFilterOptions';

const {RangePicker} = DatePicker;
const {Text} = Typography;

type AuditRow = {
  id: string;
  actorId: string | null;
  actorName: string | null;
  action: string;
  resourceId: string;
  createdAt: string;
};

const ACTION_LABELS: Record<string, string> = {
  ServerFileCreate: 'Tạo file',
  ServerFileWrite: 'Ghi file',
  ServerFileRename: 'Đổi tên file',
  ServerFileCopy: 'Sao chép file',
  ServerFileMove: 'Di chuyển file',
  ServerFileDelete: 'Xóa file',
  ServerFileRestore: 'Khôi phục file',
};

const ACTION_COLOR: Record<string, string> = {
  ServerFileCreate: 'blue',
  ServerFileWrite: 'cyan',
  ServerFileRename: 'gold',
  ServerFileCopy: 'geekblue',
  ServerFileMove: 'purple',
  ServerFileDelete: 'red',
  ServerFileRestore: 'volcano',
};

const DEMO_ROWS: AuditRow[] = [
  {id:'1', actorId:'u1', actorName:'Nguyễn Văn A', action:'ServerFileCreate', resourceId:'f1', createdAt: new Date(Date.now()-3600000).toISOString()},
  {id:'2', actorId:'u2', actorName:'Trần Thị B', action:'ServerFileWrite', resourceId:'s1', createdAt: new Date(Date.now()-7200000).toISOString()},
  {id:'3', actorId:null, actorName:null, action:'ServerFileRename', resourceId:'p1', createdAt: new Date(Date.now()-86400000).toISOString()},
  {id:'4', actorId:'u1', actorName:'Nguyễn Văn A', action:'ServerFileDelete', resourceId:'f2', createdAt: new Date(Date.now()-172800000).toISOString()},
];

export default function AuditLogPage() {
  const [rows, setRows] = useState<AuditRow[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [dateRange, setDateRange] = useState<[dayjs.Dayjs|null, dayjs.Dayjs|null]>([null, null]);
  const [actionFilter, setActionFilter] = useState('');
  const [revision, setRevision] = useState(0);
  const filters = useAdminFilterOptions();
  const [actor, setActor] = useState('');
  const [applied, setApplied] = useState({from:'', to:'', action:'', actor:''});
  const invalidDates = !!(dateRange[0] && dateRange[1] && dateRange[0].isAfter(dateRange[1]));

  const pageSize = 50;

  useEffect(() => {
    if (isDemoMode) {
      const filtered = DEMO_ROWS.filter(row => (!applied.action || row.action === applied.action)
        && (!applied.from || row.createdAt >= applied.from) && (!applied.to || row.createdAt <= applied.to));
      setRows(filtered); setTotal(filtered.length);
      return;
    }
    const c = new AbortController();
    setLoading(true); setRows([]); setTotal(0);
    setError('');
    const params = new URLSearchParams();
    params.set('page', String(page));
    params.set('pageSize', String(pageSize));
    if (applied.from) params.set('from', applied.from);
    if (applied.to) params.set('to', applied.to);
    if (applied.action) params.set('action', applied.action);
    if (applied.actor) params.set('actorId', applied.actor);

    api<PagedResult<AuditRow>>(`/admin/audit-log?${params}`, {signal: c.signal})
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
    if (invalidDates) return;
    setApplied({from: dateRange[0]?.toISOString() ?? '', to: dateRange[1]?.toISOString() ?? '', action: actionFilter, actor});
    setPage(1);
    setRevision(v => v + 1);
  }

  const columns: ColumnsType<AuditRow> = [
    {
      title: 'Thời gian',
      dataIndex: 'createdAt',
      width: 180,
      render: v => <Text style={{fontSize:13}}>{new Date(v).toLocaleString('vi-VN')}</Text>,
    },
    {
      title: 'Thao tác',
      dataIndex: 'action',
      width: 220,
      render: (v: string) => (
        <Tag color={ACTION_COLOR[v] ?? 'default'}>
          {ACTION_LABELS[v] ?? v}
        </Tag>
      ),
    },
    {
      title: 'Người thực hiện',
      dataIndex: 'actorName',
      width: 180,
      render: (v, row) => v
        ? <Text>{v}</Text>
        : <Text type="secondary">{row.actorId ? `ID: ${row.actorId.slice(0,8)}…` : 'Hệ thống'}</Text>,
    },
    {
      title: 'Mã thao tác',
      dataIndex: 'resourceId',
      ellipsis: true,
      render: v => (
        <Tooltip title={v}>
          <Text code style={{fontSize:12}}>{v.slice(0,8)}…</Text>
        </Tooltip>
      ),
    },
  ];

  return (
    <div className="page-content">
      <Typography.Paragraph>Nhật ký thao tác file. Metadata hiện tại xem ở trang Metadata file; nhật ký này chưa lưu tên và đường dẫn trước–sau của từng file.</Typography.Paragraph>
      {filters.error && <Alert type="warning" message={filters.error} action={<Button onClick={filters.retry}>Tải lại bộ lọc</Button>} style={{marginBottom:12}} />}
      {invalidDates && <Alert type="warning" message="Thời điểm kết thúc phải sau thời điểm bắt đầu." />}

      <div style={{marginBottom:16, display:'flex', gap:8, flexWrap:'wrap', alignItems:'center'}}>
        <RangePicker
          showTime
          format="DD/MM/YYYY HH:mm"
          value={dateRange}
          onChange={v => setDateRange(v ? [v[0], v[1]] : [null, null])}
          style={{width: 340}}
          placeholder={['Từ ngày', 'Đến ngày']}
        />
        <Select
          allowClear
          aria-label="Loại thao tác"
          placeholder="Loại thao tác"
          style={{width: 200}}
          value={actionFilter || undefined}
          onChange={v => setActionFilter(v ?? '')}
          options={Object.entries(ACTION_LABELS).map(([k, v]) => ({value: k, label: v}))}
        />
        {!isDemoMode && <Select aria-label="Người thực hiện" placeholder="Người thực hiện" style={{width:220}} allowClear showSearch optionFilterProp="label" loading={filters.loading} value={actor || undefined} onChange={value=>setActor(value ?? '')} options={filters.users.map(u=>({value:u.id,label:u.fullName || u.username}))} />}
        <Space>
          <Button icon={<SearchOutlined />} type="primary" aria-label="Lọc" disabled={invalidDates} onClick={search}>Lọc</Button>
          <Button icon={<ReloadOutlined />} aria-label="Đặt lại" onClick={() => { setDateRange([null,null]); setActionFilter(''); setActor(''); setApplied({from:'',to:'',action:'',actor:''}); setPage(1); setRevision(v=>v+1); }}>Đặt lại</Button>
        </Space>
      </div>
      {error && <Alert type="error" message={error} action={<Button onClick={()=>setRevision(v=>v+1)}>Thử lại</Button>} style={{marginBottom:12}} />}
      <Table<AuditRow>
        rowKey="id"
        loading={loading}
        dataSource={rows}
        columns={columns}
        pagination={{
          current: page,
          pageSize,
          total,
          showTotal: t => `${t} sự kiện`,
          onChange: p => setPage(p),
          showSizeChanger: false,
        }}
        size="middle"
        scroll={{x: 700}}
        locale={{emptyText: 'Chưa có nhật ký.'}}
      />
    </div>
  );
}
