import {useEffect, useState} from 'react';
import {Alert, Button, InputNumber, Modal, Popconfirm, Space, Table, Tag} from 'antd';
import {api} from '../api/auth';
import {isDemoMode} from '../demo/mode';

type Profile = {profileId:string; username:string; deviceName:string; maxFiles:number; usedFiles:number; reservedFiles?:number; pendingState?:string|null; storageBackend?:'Server'|'Agent'};
type Audit = {createdAt:string; action:string; actor:string|null};
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
  <h1>Quota theo nhân viên – thiết bị</h1>
  {error&&<Alert type="error" showIcon message={error}/>}
  {notice&&<Alert type="success" showIcon message={notice}/>}
  <p>Mặc định 10 file cho mỗi profile. Điều chỉnh chỉ áp dụng cho nhân viên và máy trên dòng tương ứng; file hiện có được giữ nguyên.</p>
  <p>Profile Server mô phỏng lưu file và cập nhật quota trên máy chủ trong cùng thao tác; không cần đối soát Agent.</p>
  {rows.some(row=>row.storageBackend==='Agent')&&<p>Chỉ profile Agent cũ có đối soát và nhật ký: Agent kiểm tra metadata file khi thao tác bị gián đoạn, không xóa hoặc đọc nội dung tài liệu.</p>}
  {isDemoMode&&rows.some(row=>row.storageBackend==='Agent')&&<Alert type="info" message="Đối soát và nhật ký không khả dụng trong chế độ minh họa."/>}
  <Button disabled={busy||loading} onClick={()=>{setError('');setRevision(v=>v+1)}} style={{marginBottom:16}}>Làm mới</Button>
  <Table<Profile> scroll={{x:1000}} rowKey="profileId" dataSource={rows} loading={loading} columns={[
   {title:'Tài khoản',dataIndex:'username'}, {title:'Máy',dataIndex:'deviceName'},
   {title:'Lưu trữ',dataIndex:'storageBackend',render:value=><Tag color={value==='Server'?'blue':'default'}>{value==='Server'?'Server mô phỏng':value==='Agent'?'Agent cũ':'Chưa khai báo'}</Tag>},
   {title:'Đã dùng',dataIndex:'usedFiles'}, {title:'Giữ chỗ',render:(_,r)=>r.reservedFiles??0},
   {title:'Trạng thái',render:(_,r)=>r.storageBackend==='Agent'&&r.pendingState?<Tag color="orange">{r.pendingState==='Reconciling'?'Đang đối soát':r.pendingState==='Unknown'?'Cần đối soát':'Đang xử lý'}</Tag>:<Tag color="green">Ổn định</Tag>},
   {title:'Tối đa',render:(_,r)=><InputNumber aria-label={`Quota ${r.username} ${r.deviceName}`} min={0} max={10000} precision={0} value={r.maxFiles} disabled={busy||loading} onChange={v=>{if(v!==null)setRows(rows.map(x=>x.profileId===r.profileId?{...x,maxFiles:v}:x))}}/>},
   {title:'Thao tác',render:(_,r)=><Space wrap>
    <Button disabled={busy||loading} onClick={()=>void save(r)}>Lưu quota</Button>
    {r.storageBackend==='Agent'&&<><Popconfirm title="Yêu cầu Agent cũ đối soát profile?" description="File được giữ nguyên. Quota chỉ cập nhật khi Agent trả kết quả hợp lệ." okText="Yêu cầu đối soát" cancelText="Hủy" onConfirm={()=>reconcile(r)} disabled={busy||loading||isDemoMode||!r.pendingState}>
     <Button disabled={busy||loading||isDemoMode||!r.pendingState}>Đối soát</Button>
    </Popconfirm>
    <Button disabled={busy||loading||isDemoMode} onClick={()=>void showHistory(r)}>Nhật ký đối soát</Button></>}
   </Space>},
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
