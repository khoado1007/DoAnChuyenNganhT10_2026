import { FileTextOutlined, FolderOpenOutlined, UploadOutlined } from '@ant-design/icons';
import { Alert, Button, Card, Input, Modal, Popconfirm, Select, Space, Table } from 'antd';
import type { ProfileFile } from '../api/machines';
import type { useDesktopFiles } from './useDesktopFiles';

const actionLabels = { create: 'Tạo tài liệu', rename: 'Đổi tên', move: 'Di chuyển', copy: 'Sao chép' };
const folders = ['Desktop', 'Documents'].map(value => ({value}));

export default function DesktopFiles({ files, leaving, onLeave }: {
  files: ReturnType<typeof useDesktopFiles>; leaving: boolean; onLeave: () => void;
}) {
  const { profile, folder, selected, deleted, action, run } = files;
  const { quota } = profile;
  const busy = files.busy || leaving;
  const full = !quota.remainingFiles;
  return <>
    <div className="workstation-desktop">
      <aside className="desktop-shortcuts">
        {folders.map(({value}) => <button key={value} className={folder === value ? 'active' : ''} disabled={busy}
          onClick={() => files.changeFolder(value)}><FolderOpenOutlined /><span>{value}</span></button>)}
      </aside>
      <section className="file-manager">
        <div className="file-manager-title"><FolderOpenOutlined /> File Manager <span>{folder}</span></div>
        <Card className="profile-strip">
          <b>{profile.domainId} · {profile.deviceName}</b><p>File của bạn trên máy này · Quota dùng chung trên tất cả máy</p>
          <progress aria-label="Số file đã dùng" max={quota.maxFiles || 1} value={quota.usedFiles}/>
          <p>Quota: {quota.usedFiles} / {quota.maxFiles} file · Đang giữ chỗ: {quota.reservedFiles} · Còn lại: {quota.remainingFiles}</p>
          {quota.overQuota && <Alert type="warning" message="Đã vượt quota. Xóa file hoặc liên hệ quản trị."/>}
        </Card>
        <Space wrap style={{ margin: '16px 0' }}>
          <Select disabled={busy} aria-label="Thư mục" value={folder} onChange={files.changeFolder}
            options={[...folders, {value: 'RecycleBin', label: 'Thùng rác'}]}/>
          <Button disabled={busy} onClick={() => void run({kind:'list', folder})}>Làm mới</Button>
          {folder !== 'RecycleBin' && <>
            <Button disabled={busy || full} onClick={() => files.openDialog('create')}>Tạo TXT/CSV</Button>
            <label className="file-import"><UploadOutlined /> Nhập tài liệu
              <input type="file" aria-label="Nhập tài liệu" disabled={busy || full} accept=".doc,.docx,.xls,.xlsx,.ppt,.pptx,.pdf,.txt,.csv,.ini"
                onChange={event => {
                  const file = event.target.files?.[0];
                  event.target.value = '';
                  if (file) void files.importFile(file);
                }}/>
            </label>
          </>}
        </Space>
        {deleted && <Button disabled={busy || full} onClick={() => void run({kind:'restore', fileId:deleted, targetFolder:'Documents'})}>
          Khôi phục file vừa xóa vào Documents</Button>}
        <Table<ProfileFile> scroll={{x:480}} rowKey="fileId" loading={busy} dataSource={profile.files.filter(file => file.folder === folder)}
          rowSelection={{ type:'radio', getCheckboxProps:() => ({disabled:busy}), selectedRowKeys:selected ? [selected.fileId] : [],
            onChange: (_, rows) => files.select(rows[0] ?? null) }}
          columns={[
            {title:'Tên file', dataIndex:'name', render:(value:string) => <span><FileTextOutlined style={{color:'#286b94', marginRight:8}}/>{value}</span>},
            {title:'Kích thước (byte)', dataIndex:'sizeBytes'}, {title:'Loại', dataIndex:'extension'},
          ]} locale={{emptyText:'Thư mục trống'}}/>
        {selected && <Space wrap>{folder === 'RecycleBin'
          ? <Button disabled={busy || full} onClick={() => void run({kind:'restore', fileId:selected.fileId, targetFolder:'Documents'})}>Khôi phục vào Documents</Button>
          : <>
            <Button disabled={busy} onClick={() => void run({kind:'read', fileId:selected.fileId})}>Mở / tải xuống</Button>
            {(['rename','move','copy'] as const).map(kind => <Button key={kind} disabled={busy || (kind === 'copy' && full)}
              onClick={() => files.openDialog(kind)}>{actionLabels[kind]}</Button>)}
            <Popconfirm title="Chuyển file vào thùng rác?" onConfirm={() => run({kind:'delete', fileId:selected.fileId})}>
              <Button danger disabled={busy}>Xóa</Button>
            </Popconfirm>
          </>}</Space>}
        {files.text !== null && selected && <Card title={selected.name} style={{marginTop:16}}>
          <Input.TextArea aria-label="Nội dung TXT/CSV" rows={12} value={files.text} onChange={e => files.setText(e.target.value)} disabled={busy}/>
          <Button loading={busy} onClick={() => void files.saveText()}>Lưu nội dung</Button>
        </Card>}
        {files.pdf && <Card title={selected?.name}><a href={files.pdf} download={selected?.name}>Tải PDF</a>
          <iframe title="Xem PDF" src={files.pdf} style={{width:'100%', height:600, border:0}}/></Card>}
      </section>
    </div>
    <footer className="workstation-taskbar"><span><FolderOpenOutlined /> File Manager</span>
      <span>{profile.deviceName} · {profile.domainId}</span><Button disabled={busy} onClick={onLeave}>Đăng xuất</Button>
    </footer>
    <Modal title={action && actionLabels[action]} open={!!action} confirmLoading={busy}
      onCancel={() => { if (!busy) files.closeDialog(); }} onOk={files.submitDialog}>
      <Input aria-label="Tên file" value={files.name} onChange={e => files.setName(e.target.value)}/>
      {(action === 'move' || action === 'copy') && <Select aria-label="Thư mục đích" value={files.target} onChange={files.setTarget} options={folders}/>}
    </Modal>
  </>;
}
