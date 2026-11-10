import {useEffect,useState} from 'react';
import {Alert,Button,Modal,Popconfirm,Select,Space,Table} from 'antd';
import {api} from '../api/auth';
type Access={userId:string;username:string;fullName:string};
type User={id:string;username:string;fullName:string};
export default function DeviceAccessModal({deviceId,onClose}:{deviceId:string;onClose:()=>void}){
 const [rows,setRows]=useState<Access[]>([]),[users,setUsers]=useState<User[]>([]),[userId,setUserId]=useState<string>(),[error,setError]=useState(''),[busy,setBusy]=useState(false),[revision,setRevision]=useState(0),[search,setSearch]=useState('');
 useEffect(()=>{const c=new AbortController();Promise.all([api<Access[]>(`/assets/${deviceId}/access`,{signal:c.signal}),api<{items:User[]}>(`/users?page=1&pageSize=50&search=${encodeURIComponent(search)}`,{signal:c.signal})]).then(([r,u])=>{if(!c.signal.aborted){setRows(r);setUsers(u.items)}}).catch(e=>{if(!c.signal.aborted)setError(e.message)});return()=>c.abort()},[deviceId,revision,search]);
 async function change(id:string,method:string){setBusy(true);setError('');try{await api(`/assets/${deviceId}/access/${id}`,{method,...(method==='PUT'?{body:'{}'}:{})});setRevision(v=>v+1)}catch(e){setError((e as Error).message)}finally{setBusy(false)}}
 return <Modal title="Quyền đăng nhập máy" open onCancel={()=>{if(!busy)onClose()}} footer={null}><p>Quyền sử dụng độc lập với người chịu trách nhiệm tài sản.</p>{error&&<Alert type="error" message={error}/>}<Space wrap style={{marginBottom:16}}><Select aria-label="Chọn tài khoản" style={{width:280}} value={userId} onChange={setUserId} showSearch filterOption={false} onSearch={setSearch} options={users.map(u=>({value:u.id,label:`${u.username} · ${u.fullName}`}))}/><Button disabled={!userId||busy} onClick={()=>void change(userId!,'PUT')}>Cấp quyền</Button></Space><Table<Access> scroll={{x:420}} rowKey="userId" dataSource={rows} columns={[{title:'Tài khoản',dataIndex:'username'},{title:'Họ tên',dataIndex:'fullName'},{title:'Thao tác',render:(_:unknown,r:Access)=><Popconfirm title="Thu hồi quyền đăng nhập máy?" description="Profile và file hiện có được giữ lại." onConfirm={()=>change(r.userId,'DELETE')}><Button danger disabled={busy}>Thu hồi</Button></Popconfirm>}]}/></Modal>
}

