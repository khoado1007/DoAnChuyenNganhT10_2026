import {useEffect, useState} from 'react';
import {Alert, Button, InputNumber, Modal, Popconfirm, Space, Table, Tag} from 'antd';
import {api} from '../api/auth';
import {isDemoMode} from '../demo/mode';

type Profile = {profileId:string; username:string; deviceName:string; maxFiles:number; usedFiles:number; reservedFiles?:number; pendingState?:string|null; storageBackend?:'Server'|'Agent'};
type Audit = {createdAt:string; action:string; actor:string|null};
type AccountQuota = { username: string; profiles: Profile[]; used: number; reserved: number };
function groupByAccount(rows: Profile[]): AccountQuota[] {
 const accounts = new Map<string, AccountQuota>();
 for (const profile of rows) {
  const account = accounts.get(profile.username) ?? { username: profile.username, profiles: [], used: 0, reserved: 0 };
  account.profiles.push(profile);
  account.used += profile.usedFiles;
  account.reserved += profile.reservedFiles ?? 0;
  accounts.set(profile.username, account);
 }
 return [...accounts.values()];
}
const actions:Record<string,string> = {
 ProfileReconciliationRequested:'Yêu cầu đối soát',
 ProfileReconciliationCompleted:'Hoàn tất đối soát',
 ProfileReconciliationFailed:'Đối soát chưa thành công',
};
export default function QuotasPage(){
 const [rows,setRows]=useState<Profile[]>([]),[error,setError]=useState(''),[notice,setNotice]=useState('');
 const [busy,setBusy]=useState(false),[loading,setLoading]=useState(false),[revision,setRevision]=useState(0);
 const [history,setHistory]=useState<Audit[]>([]),[historyTitle,setHistoryTitle]=useState('');
 useEffect(()=>{
  const c=new AbortController();setLoading(true);
  api<Profile[]>('/profiles',{signal:c.signal}).then(d=>{if(!c.signal.aborted)setRows(d)})
   .catch(e=>{if(!c.signal.aborted)setError(e.message)})
   .finally(()=>{if(!c.signal.aborted)setLoading(false)});
  return()=>c.abort();
 },[revision]);
 async function save(row:Profile){
  setBusy(true);setError('');setNotice('');
  try{await api(`/profiles/${row.profileId}/quota`,{method:'PUT',body:JSON.stringify({maxFiles:row.maxFiles})});setNotice('Đã lưu quota.');setRevision(v=>v+1)}
  catch(e){setError((e as Error).message)}finally{setBusy(false)}
 }
 async function reconcile(row:Profile){
  setBusy(true);setError('');setNotice('');
  try{await api(`/profiles/${row.profileId}/reconcile`,{method:'POST',body:'{}'});setNotice('Đối soát hoàn tất. Quota đã được cập nhật theo file hiện có.')}
  catch(e){setError((e as Error).message)}
  finally{setBusy(false);setRevision(v=>v+1)}
 }
 async function showHistory(row:Profile){
  setBusy(true);setError('');
  try{const result=await api<Audit[]>(`/profiles/${row.profileId}/reconciliation-history`);setHistory(result);setHistoryTitle(`Nhật ký · ${row.username} · ${row.deviceName}`)}
  catch(e){setError((e as Error).message)}finally{setBusy(false)}
 }
 return <main className="page-content">
  <h1>Quota theo tài khoản</h1>
  {error&&<Alert type="error" showIcon message={error}/>}
  {notice&&<Alert type="success" showIcon message={notice}/>}
  <p>Mặc định mỗi tài khoản được lưu tổng cộng 10 file trên tất cả máy. File trong thùng rác và phần mềm do Admin thêm được miễn quota.</p>
  {rows.some(row=>row.storageBackend==='Agent')&&<p>Chỉ profile Agent cũ có đối soát và nhật ký: Agent kiểm tra metadata file khi thao tác bị gián đoạn, không xóa hoặc đọc nội dung tài liệu.</p>}
  {isDemoMode&&rows.some(row=>row.storageBackend==='Agent')&&<Alert type="info" message="Đối soát và nhật ký không khả dụng trong chế độ minh họa."/>}
  <Button disabled={busy||loading} onClick={()=>{setError('');setRevision(v=>v+1)}} style={{marginBottom:16}}>Làm mới</Button>
  <Table<AccountQuota> scroll={{x:850}} rowKey="username" dataSource={groupByAccount(rows)} loading={loading} columns={[
   {title:'Tài khoản',dataIndex:'username'},
   {title:'File theo máy',render:(_,account)=>account.profiles.map(p=><div key={p.profileId}>
    {p.deviceName} · {p.usedFiles} file <Tag>{p.storageBackend==='Server'?'Server mô phỏng':p.storageBackend==='Agent'?'Agent cũ':'Chưa khai báo'}</Tag>
    {p.storageBackend==='Agent'&&<Space wrap>
     {p.pendingState&&<Tag color="orange">{p.pendingState==='Reconciling'?'Đang đối soát':p.pendingState==='Unknown'?'Cần đối soát':'Đang xử lý'}</Tag>}
     <Popconfirm title={`Đối soát file trên ${p.deviceName}?`} okText="Yêu cầu đối soát" cancelText="Hủy" onConfirm={()=>reconcile(p)} disabled={busy||loading||isDemoMode||!p.pendingState}>
      <Button disabled={busy||loading||isDemoMode||!p.pendingState}>Đối soát</Button>
     </Popconfirm>
     <Button disabled={busy||loading||isDemoMode} onClick={()=>void showHistory(p)}>Nhật ký đối soát</Button>
    </Space>}
   </div>)},
   {title:'Tổng đã dùng',dataIndex:'used'}, {title:'Giữ chỗ',dataIndex:'reserved'},
   {title:'Giới hạn tài khoản',render:(_,account)=><InputNumber aria-label={`Quota ${account.username}`} min={0} max={10000} precision={0} value={account.profiles[0].maxFiles} disabled={busy||loading}
    onChange={v=>{if(v!==null)setRows(previous=>previous.map(p=>p.username===account.username?{...p,maxFiles:v}:p))}}/>},
   {title:'Thao tác',render:(_,account)=><Button disabled={busy||loading} onClick={()=>void save(account.profiles[0])}>Lưu quota</Button>},
  ]}/>
  <Modal title={historyTitle} open={!!historyTitle} onCancel={()=>setHistoryTitle('')} footer={<Button onClick={()=>setHistoryTitle('')}>Đóng</Button>} width={760}>
   <Table<Audit> size="small" scroll={{x:550}} rowKey={r=>`${r.createdAt}-${r.action}-${r.actor}`} dataSource={history} pagination={false} locale={{emptyText:'Chưa có yêu cầu đối soát.'}} columns={[
    {title:'Thời gian',render:(_,r)=>new Date(r.createdAt.endsWith('Z')?r.createdAt:r.createdAt+'Z').toLocaleString('vi-VN')},
    {title:'Thao tác',render:(_,r)=>actions[r.action]??r.action},
    {title:'Người yêu cầu',render:(_,r)=>r.actor??'Tài khoản không còn khả dụng'},
   ]}/>
  </Modal>
 </main>
}
