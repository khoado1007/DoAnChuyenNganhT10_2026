import {useEffect, useState} from 'react';
import {Alert, Button, Card, Checkbox, Descriptions, Divider, Input, List, Modal, Popconfirm, Select, Space, Spin, Table, Tag, Typography} from 'antd';
import {ReloadOutlined, SendOutlined} from '@ant-design/icons';
import type {ColumnsType} from 'antd/es/table';
import {api} from '../api/auth';
import type {PagedResult} from '../types/api';
import {
  TICKET_STATUSES, categoryLabel, formatTicketTime, nextTicketAction, priorityColor, priorityLabel,
  statusColor, statusLabel,
  type TicketCategory, type TicketDetail, type TicketListItem, type TicketPriority, type TicketStatus,
} from '../tickets';

const {Text, Paragraph} = Typography;
const {TextArea} = Input;
const PAGE_SIZE = 20;

type Filters = {search: string; status: TicketStatus | ''; category: TicketCategory | ''; priority: TicketPriority | ''};
const EMPTY_FILTERS: Filters = {search: '', status: '', category: '', priority: ''};

const categoryOptions = (['Hardware', 'Software', 'Network', 'Account', 'Storage', 'Other'] as const)
  .map(value => ({value, label: categoryLabel(value)}));
const priorityOptions = (['Low', 'Medium', 'High', 'Critical'] as const)
  .map(value => ({value, label: priorityLabel(value)}));

/** Khu vực kỹ thuật viên: xem và xử lý các ticket được phân công. */
export default function TechnicianTickets() {
  const [rows, setRows] = useState<TicketListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [revision, setRevision] = useState(0);
  const [draft, setDraft] = useState<Filters>(EMPTY_FILTERS);
  const [applied, setApplied] = useState<Filters>(EMPTY_FILTERS);

  const [detail, setDetail] = useState<TicketDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [actionBusy, setActionBusy] = useState(false);
  const [comment, setComment] = useState('');
  const [internal, setInternal] = useState(false);
  const [resolution, setResolution] = useState('');
  const [note, setNote] = useState('');

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setRows([]); setError('');
    const params = new URLSearchParams({page: String(page), pageSize: String(PAGE_SIZE)});
    if (applied.search) params.set('search', applied.search);
    if (applied.status) params.set('status', applied.status);
    if (applied.category) params.set('category', applied.category);
    if (applied.priority) params.set('priority', applied.priority);

    api<PagedResult<TicketListItem>>(`/tickets?${params.toString()}`, {signal: controller.signal})
      .then(data => {
        if (!controller.signal.aborted) {
          setRows(data.items);
          setTotal(data.totalCount);
        }
      })
      .catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });

    return () => controller.abort();
  }, [page, applied, revision]);

  function search() { setPage(1); setApplied(draft); }
  function reset() { setDraft(EMPTY_FILTERS); setApplied(EMPTY_FILTERS); setPage(1); }

  async function openDetail(id: string) {
    setDetailLoading(true); setDetail(null); setDetailError('');
    setComment(''); setInternal(false); setResolution(''); setNote('');
    try { setDetail(await api<TicketDetail>(`/tickets/${id}`)); }
    catch (cause) { setDetailError((cause as Error).message); }
    finally { setDetailLoading(false); }
  }

  async function submitStatus() {
    if (!detail) return;
    const next = nextTicketAction(detail.status);
    if (!next) return;
    if (next.needsResolution && !resolution.trim()) {
      setDetailError('Vui lòng nhập kết quả xử lý trước khi hoàn tất yêu cầu.');
      return;
    }
    setActionBusy(true); setDetailError('');
    try {
      const updated = await api<TicketDetail>(`/tickets/${detail.id}/status`, {
        method: 'PATCH',
        body: JSON.stringify({
          status: next.status,
          resolution: next.needsResolution ? resolution.trim() : null,
          note: note.trim() || null,
        }),
      });
      setNotice(`Đã chuyển ticket sang ${statusLabel(next.status)}.`);
      setDetail(updated);
      setResolution(''); setNote('');
      setRevision(value => value + 1);
    } catch (cause) { setDetailError((cause as Error).message); }
    finally { setActionBusy(false); }
  }

  async function submitComment() {
    if (!detail || !comment.trim()) return;
    setActionBusy(true); setDetailError('');
    try {
      await api(`/tickets/${detail.id}/comments`, {
        method: 'POST',
        body: JSON.stringify({content: comment.trim(), isInternal: internal}),
      });
      setComment(''); setInternal(false);
      setDetail(await api<TicketDetail>(`/tickets/${detail.id}`));
    } catch (cause) { setDetailError((cause as Error).message); }
    finally { setActionBusy(false); }
  }

  const columns: ColumnsType<TicketListItem> = [
    {title: 'Mã', dataIndex: 'ticketNumber', width: 130, render: value => <Text code>{value}</Text>},
    {
      title: 'Tiêu đề', dataIndex: 'title', ellipsis: true,
      render: (value, row) => <button className="table-link" onClick={() => void openDetail(row.id)}>{value}</button>,
    },
    {title: 'Danh mục', dataIndex: 'category', width: 110, render: value => categoryLabel(value)},
    {title: 'Ưu tiên', dataIndex: 'priority', width: 100, render: value => <Tag color={priorityColor(value)}>{priorityLabel(value)}</Tag>},
    {title: 'Trạng thái', dataIndex: 'status', width: 120, render: value => <Tag color={statusColor(value)}>{statusLabel(value)}</Tag>},
    {title: 'Người tạo', dataIndex: 'createdByName', width: 160},
    {title: 'Cập nhật', dataIndex: 'updatedAt', width: 170, render: (value, row) => formatTicketTime(value ?? row.createdAt)},
    {title: '', key: 'actions', width: 100, render: (_, row) => <Button size="small" type="primary" ghost onClick={() => void openDetail(row.id)}>Xem & xử lý</Button>},
  ];

  const next = detail ? nextTicketAction(detail.status) : null;

  return <Card title="Yêu cầu hỗ trợ được phân công" style={{ marginTop: 16 }}>
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16, flexWrap: 'wrap', gap: 8 }}>
      <Paragraph style={{ margin: 0 }}>
        Danh sách các yêu cầu hỗ trợ người dùng được quản trị viên phân công cho bạn xử lý.
      </Paragraph>
      <Button icon={<ReloadOutlined />} aria-label="Làm mới" onClick={() => setRevision(value => value + 1)}>Làm mới</Button>
    </div>

    {notice && <Alert type="success" showIcon message={notice} closable onClose={() => setNotice('')} style={{marginBottom: 16}} />}
    {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} style={{marginBottom: 16}} />}

    <Space wrap style={{marginBottom: 16}}>
      <Input.Search aria-label="Tìm ticket" allowClear maxLength={100} placeholder="Mã hoặc tiêu đề" style={{width: 240}}
        value={draft.search} onChange={event => setDraft({...draft, search: event.target.value})}
        onSearch={() => search()} />
      <Select aria-label="Trạng thái ticket" allowClear placeholder="Trạng thái" style={{width: 150}}
        value={draft.status || undefined} onChange={value => setDraft({...draft, status: value ?? ''})}
        options={TICKET_STATUSES.map(value => ({value, label: statusLabel(value)}))} />
      <Select aria-label="Danh mục ticket" allowClear placeholder="Danh mục" style={{width: 150}}
        value={draft.category || undefined} onChange={value => setDraft({...draft, category: value ?? ''})}
        options={categoryOptions} />
      <Select aria-label="Ưu tiên ticket" allowClear placeholder="Ưu tiên" style={{width: 140}}
        value={draft.priority || undefined} onChange={value => setDraft({...draft, priority: value ?? ''})}
        options={priorityOptions} />
      <Button type="primary" onClick={search}>Lọc</Button>
      <Button onClick={reset}>Đặt lại</Button>
    </Space>

    <Table<TicketListItem>
      rowKey="id"
      columns={columns}
      dataSource={rows}
      loading={loading}
      pagination={{
        current: page,
        pageSize: PAGE_SIZE,
        total,
        showSizeChanger: false,
        onChange: p => setPage(p),
      }}
      scroll={{x: 880}}
      locale={{emptyText: 'Chưa có yêu cầu hỗ trợ nào được phân công.'}}
    />

    <Modal
      open={detailLoading || !!detail}
      title={detail ? `${detail.ticketNumber} · ${detail.title}` : 'Chi tiết yêu cầu'}
      width={820}
      onCancel={() => { if (!actionBusy) { setDetail(null); setDetailError(''); } }}
      footer={<Button onClick={() => setDetail(null)}>Đóng</Button>}
      destroyOnClose
    >
      {detailLoading && <div style={{textAlign: 'center', padding: 32}}><Spin /><p>Đang tải chi tiết…</p></div>}
      {detailError && <Alert type="error" showIcon message={detailError} style={{marginBottom: 12}} />}
      {detail && <>
        <Descriptions column={2} size="small" bordered items={[
          {key: 'status', label: 'Trạng thái', children: <Tag color={statusColor(detail.status)}>{statusLabel(detail.status)}</Tag>},
          {key: 'priority', label: 'Ưu tiên', children: <Tag color={priorityColor(detail.priority)}>{priorityLabel(detail.priority)}</Tag>},
          {key: 'category', label: 'Danh mục', children: categoryLabel(detail.category)},
          {key: 'createdBy', label: 'Người tạo', children: detail.createdByName},
          {key: 'createdAt', label: 'Ngày tạo', children: formatTicketTime(detail.createdAt)},
          {key: 'updatedAt', label: 'Cập nhật', children: formatTicketTime(detail.updatedAt ?? detail.createdAt)},
          {key: 'assignee', label: 'Người xử lý', children: detail.assignedToName ?? 'Chưa phân công'},
          {key: 'resolvedAt', label: 'Hoàn tất lúc', children: detail.resolvedAt ? formatTicketTime(detail.resolvedAt) : '—'},
        ]} />

        <Divider orientation="left">Nội dung yêu cầu</Divider>
        <Paragraph style={{whiteSpace: 'pre-wrap', marginBottom: 0}}>{detail.description}</Paragraph>
        {detail.resolution && <Alert style={{marginTop: 16}} type="success" message="Kết quả xử lý" description={detail.resolution} />}

        <Divider orientation="left">Trao đổi ({detail.comments.length})</Divider>
        <List
          dataSource={detail.comments}
          locale={{emptyText: 'Chưa có trao đổi.'}}
          renderItem={item => <List.Item>
            <List.Item.Meta
              title={<Space><Text strong>{item.authorUsername}</Text>{item.isInternal && <Tag color="orange">Nội bộ</Tag>}</Space>}
              description={<><Text type="secondary" style={{fontSize: 12}}>{formatTicketTime(item.createdAt)}</Text><Paragraph style={{margin: '4px 0 0', whiteSpace: 'pre-wrap'}}>{item.content}</Paragraph></>}
            />
          </List.Item>}
        />
        <Space.Compact style={{width: '100%', marginTop: 8}}>
          <TextArea aria-label="Nội dung bình luận" rows={2} maxLength={2000} value={comment}
            onChange={event => setComment(event.target.value)} placeholder="Trả lời người tạo hoặc ghi chú xử lý" />
        </Space.Compact>
        <Space style={{marginTop: 8, width: '100%', justifyContent: 'space-between'}} wrap>
          <Checkbox checked={internal} onChange={event => setInternal(event.target.checked)}>Ghi chú nội bộ (nhân viên không thấy)</Checkbox>
          <Button type="primary" icon={<SendOutlined />} aria-label="Gửi bình luận" loading={actionBusy} disabled={!comment.trim()} onClick={() => void submitComment()}>Gửi bình luận</Button>
        </Space>

        <Divider orientation="left">Lịch sử trạng thái</Divider>
        <List size="small" dataSource={detail.history} locale={{emptyText: 'Chưa có chuyển trạng thái.'}}
          renderItem={item => <List.Item>
            <Text>{statusLabel(item.fromStatus)} → <Text strong>{statusLabel(item.toStatus)}</Text> · {item.changedByUsername} · {formatTicketTime(item.changedAt)}</Text>
            {item.note && <Text type="secondary">Ghi chú: {item.note}</Text>}
          </List.Item>} />

        {next && <div className="ticket-action-panel">
          <Divider orientation="left">Xử lý ticket</Divider>
          {next.needsResolution && <TextArea aria-label="Kết quả xử lý" rows={3} maxLength={4000} value={resolution}
            onChange={event => setResolution(event.target.value)} placeholder="Mô tả kết quả xử lý (bắt buộc khi hoàn tất)" style={{marginBottom: 8}} />}
          <Input aria-label="Ghi chú chuyển trạng thái" maxLength={500} value={note}
            onChange={event => setNote(event.target.value)} placeholder="Ghi chú cho lần chuyển trạng thái (không bắt buộc)" style={{marginBottom: 8}} />
          <Popconfirm title={`Chuyển sang ${statusLabel(next.status)}?`} okText="Xác nhận" cancelText="Hủy" onConfirm={() => void submitStatus()}>
            <Button type="primary" loading={actionBusy}>{next.label}</Button>
          </Popconfirm>
        </div>}
      </>}
    </Modal>
  </Card>;
}
