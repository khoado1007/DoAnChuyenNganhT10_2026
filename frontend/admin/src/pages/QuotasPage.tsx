import {useEffect,useState} from 'react';
import {Alert,Button,InputNumber,Table} from 'antd';
import {api} from '../api/auth';
type Profile={profileId:string;username:string;deviceName:string;maxFiles:number;usedFiles:number};
export default function QuotasPage(){
 const [rows,setRows]=useState<Profile[]>([]),[error,setError]=useState(''),[busy,setBusy]=useState(false),[revision,setRevision]=useState(0);
 useEffect(()=>{const c=new AbortController();api<Profile[]>('/profiles',{signal:c.signal}).then(d=>{if(!c.signal.aborted)setRows(d)}).catch(e=>{if(!c.signal.aborted)setError(e.message)});return()=>c.abort()},[revision]);
 async function save(row:Profile){setBusy(true);setError('');try{await api(`/profiles/${row.profileId}/quota`,{method:'PUT',body:JSON.stringify({maxFiles:row.maxFiles})});setRevision(v=>v+1)}catch(e){setError((e as Error).message)}finally{setBusy(false)}}
 return <main className="page-content"><h1>Quota theo nhân viên – thiết bị</h1>{error&&<Alert type="error" message={error}/>}<p>Mặc định 10 file cho mỗi profile. Điều chỉnh chỉ áp dụng cho nhân viên và máy trên dòng tương ứng; file hiện có được giữ nguyên.</p><Table scroll={{x:650}} rowKey="profileId" dataSource={rows} columns={[{title:'Tài khoản',dataIndex:'username'},{title:'Máy',dataIndex:'deviceName'},{title:'Đã dùng',dataIndex:'usedFiles'},{title:'Tối đa',render:(_:unknown,r:Profile)=><InputNumber aria-label={`Quota ${r.username} ${r.deviceName}`} min={0} precision={0} value={r.maxFiles} disabled={busy} onChange={v=>{if(v!==null)setRows(rows.map(x=>x.profileId===r.profileId?{...x,maxFiles:v}:x))}}/>},{title:'Thao tác',render:(_:unknown,r:Profile)=><Button disabled={busy} onClick={()=>void save(r)}>Lưu quota</Button>}]}/></main>
}

