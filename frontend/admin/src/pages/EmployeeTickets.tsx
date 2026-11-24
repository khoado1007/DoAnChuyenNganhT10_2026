import {useEffect, useState} from 'react';
import {Alert, Button, Card, Form, Input, List, Modal, Select, Space, Spin, Table, Tag, Typography} from 'antd';
import {PlusOutlined, SendOutlined} from '@ant-design/icons';
import type {ColumnsType} from 'antd/es/table';
import {api} from '../api/auth';
import type {PagedResult} from '../types/api';
import {
  categoryLabel, formatTicketTime, priorityColor, priorityLabel, statusColor, statusLabel,
  type TicketCategory, type TicketDetail, type TicketListItem, type TicketPriority,
} from '../tickets';

const {Text, Paragraph} = Typography;
const {TextArea} = Input;

const categoryOptions = (['Hardware', 'Software', 'Network', 'Account', 'Storage', 'Other'] as const)
  .map(value => ({value, label: categoryLabel(value)}));
const priorityOptions = (['Low', 'Medium', 'High', 'Critical'] as const)
  .map(value => ({value, label: priorityLabel(value)}));

type CreateValues = {title: string; description: string; category: TicketCategory; priority: TicketPriority};

/** Khu vực nhân viên: gửi yêu cầu hỗ trợ và theo dõi phản hồi của bộ phận CNTT. */
export default function EmployeeTickets() {
  const [rows, setRows] = useState<TicketListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const [notice, setNotice] = useState('');
  const [revision, setRevision] = useState(0);
  const [creating, setCreating] = useState(false);
  const [saving, setSaving] = useState(false);
  const [createError, setCreateError] = useState('');
  const [form] = Form.useForm<CreateValues>();

  const [detail, setDetail] = useState<TicketDetail | null>(null);
  const [detailLoading, setDetailLoading] = useState(false);
  const [detailError, setDetailError] = useState('');
  const [comment, setComment] = useState('');
  const [commentBusy, setCommentBusy] = useState(false);

  useEffect(() => {
    const controller = new AbortController();
    setLoading(true); setError('');
    api<PagedResult<TicketListItem>>(`/tickets?page=${page}&pageSize=20`, {signal: controller.signal})
      .then(data => { if (!controller.signal.aborted) { setRows(data.items); setTotal(data.totalCount); } })
      .catch(cause => { if (!controller.signal.aborted) setError((cause as Error).message); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [page, revision]);

  async function openDetail(id: string) {
    setDetailLoading(true); setDetail(null); setDetailError(''); setComment('');
    try { setDetail(await api<TicketDetail>(`/tickets/${id}`)); }
    catch (cause) { setDetailError((cause as Error).message); }
    finally { setDetailLoading(false); }
  }

  async function submitCreate(values: CreateValues) {
    setSaving(true); setCreateError('');
    try {
      const created = await api<TicketDetail>('/tickets', {method: 'POST', body: JSON.stringify(values)});
      setCreating(false); form.resetFields();
      setNotice(`Đã gửi ${created.ticketNumber}. Bộ phận CNTT sẽ phản hồi trong ticket.`);
      setPage(1); setRevision(value => value + 1);
    } catch (cause) { setCreateError((cause as Error).message); }
    finally { setSaving(false); }
  }

  async function submitComment() {
    if (!detail || !comment.trim()) return;
    setCommentBusy(true); setDetailError('');
    try {
      await api(`/tickets/${detail.id}/comments`, {method: 'POST', body: JSON.stringify({content: comment.trim()})});
      setComment('');
      setDetail(await api<TicketDetail>(`/tickets/${detail.id}`));
    } catch (cause) { setDetailError((cause as Error).message); }
    finally { setCommentBusy(false); }
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
    {title: 'Cập nhật', dataIndex: 'updatedAt', width: 170, render: (value, row) => formatTicketTime(value ?? row.createdAt)},
    {title: '', key: 'actions', width: 80, render: (_, row) => <Button size="small" onClick={() => void openDetail(row.id)}>Xem</Button>},
  ];

  return <>
    <Card
      title="Yêu cầu hỗ trợ"
      style={{marginTop: 16}}
      extra={<Button type="primary" icon={<PlusOutlined />} aria-label="Gửi yêu cầu" onClick={() => { setCreateError(''); setCreating(true); }}>Gửi yêu cầu</Button>}
    >
      <Paragraph type="secondary">Gửi sự cố về phần cứng, phần mềm, mạng, tài khoản hoặc lưu trữ. Chỉ bạn và bộ phận CNTT thấy nội dung ticket của mình.</Paragraph>
      {notice && <Alert type="success" showIcon message={notice} closable onClose={() => setNotice('')} style={{marginBottom: 12}} />}
      {error && <Alert type="error" showIcon message={error} action={<Button onClick={() => setRevision(value => value + 1)}>Thử lại</Button>} style={{marginBottom: 12}} />}
      <Table<TicketListItem>
        rowKey="id" size="small" loading={loading} dataSource={rows} columns={columns}
        scroll={{x: 820}} locale={{emptyText: 'Bạn chưa gửi yêu cầu hỗ trợ nào.'}}
        pagination={{current: page, pageSize: 20, total, showSizeChanger: false,
          showTotal: count => `${count} yêu cầu`, onChange: setPage}}
      />
    </Card>

    <Modal
      open={creating}
      title="Gửi yêu cầu hỗ trợ"
      okText="Gửi yêu cầu"
      cancelText="Hủy"
      confirmLoading={saving}
      onCancel={() => { if (!saving) { setCreating(false); setCreateError(''); } }}
      onOk={() => form.submit()}
      destroyOnClose
    >
      {createError && <Alert type="error" showIcon message={createError} style={{marginBottom: 12}} />}
      <Form form={form} layout="vertical" onFinish={values => void submitCreate(values)} initialValues={{priority: 'Medium', category: 'Hardware'}}>
        <Form.Item name="title" label="Tiêu đề" rules={[{required: true, min: 5, max: 500, message: 'Tiêu đề tối thiểu 5 ký tự.'}]}>
          <Input maxLength={500} placeholder="Ví dụ: Máy tính không kết nối được mạng" />
        </Form.Item>
        <Form.Item name="description" label="Mô tả sự cố" rules={[{required: true, min: 10, max: 4000, message: 'Mô tả tối thiểu 10 ký tự.'}]}>
          <TextArea rows={4} maxLength={4000} showCount placeholder="Mô tả hiện tượng, thời điểm và những gì bạn đã thử." />
        </Form.Item>
        <Space size="large" wrap>
          <Form.Item name="category" label="Danh mục" rules={[{required: true}]}><Select style={{width: 200}} options={categoryOptions} /></Form.Item>
          <Form.Item name="priority" label="Ưu tiên" rules={[{required: true}]}><Select style={{width: 180}} options={priorityOptions} /></Form.Item>
        </Space>
      </Form>
    </Modal>

    <Modal
      open={detailLoading || !!detail}
      title={detail ? `${detail.ticketNumber} · ${detail.title}` : 'Chi tiết yêu cầu'}
      width={780}
      footer={<Button onClick={() => setDetail(null)}>Đóng</Button>}
      onCancel={() => setDetail(null)}
      destroyOnClose
    >
      {detailLoading && <div style={{textAlign: 'center', padding: 32}}><Spin /><p>Đang tải chi tiết…</p></div>}
      {detailError && <Alert type="error" showIcon message={detailError} style={{marginBottom: 12}} />}
      {detail && <>
        <Space wrap style={{marginBottom: 12}}>
          <Tag color={statusColor(detail.status)}>{statusLabel(detail.status)}</Tag>
          <Tag color={priorityColor(detail.priority)}>Ưu tiên {priorityLabel(detail.priority).toLowerCase()}</Tag>
          <Text type="secondary">{categoryLabel(detail.category)} · Gửi lúc {formatTicketTime(detail.createdAt)}</Text>
        </Space>
        <Paragraph style={{whiteSpace: 'pre-wrap'}}>{detail.description}</Paragraph>
        {detail.resolution && <Alert type="success" message="Kết quả xử lý" description={detail.resolution} style={{marginBottom: 12}} />}
        <Typography.Title level={5}>Trao đổi</Typography.Title>
        <List
          dataSource={detail.comments.filter(item => !item.isInternal)}
          locale={{emptyText: 'Chưa có phản hồi.'}}
          renderItem={item => <List.Item>
            <List.Item.Meta
              title={<Text strong>{item.authorUsername}</Text>}
              description={<><Text type="secondary" style={{fontSize: 12}}>{formatTicketTime(item.createdAt)}</Text><Paragraph style={{margin: '4px 0 0', whiteSpace: 'pre-wrap'}}>{item.content}</Paragraph></>}
            />
          </List.Item>}
        />
        <TextArea aria-label="Nội dung bình luận" rows={2} maxLength={2000} value={comment}
          onChange={event => setComment(event.target.value)} placeholder="Bổ sung thông tin cho bộ phận CNTT" />
        <Button type="primary" icon={<SendOutlined />} aria-label="Gửi bình luận" style={{marginTop: 8}} loading={commentBusy}
          disabled={!comment.trim()} onClick={() => void submitComment()}>Gửi bình luận</Button>
      </>}
    </Modal>
  </>;
}
