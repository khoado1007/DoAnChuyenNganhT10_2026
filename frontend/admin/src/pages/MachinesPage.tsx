import {DesktopOutlined,FolderOpenOutlined,FileTextOutlined,UploadOutlined} from '@ant-design/icons';
import {isDemoMode} from '../demo/mode';
import {useEffect,useRef,useState} from 'react';
import {Alert,Button,Card,Input,Modal,Popconfirm,Select,Space,Table,Tag,Typography} from 'antd';
import {api,clearSession,getToken,deviceSessions,reportMachinePage,endMachineSession} from '../api/auth';
import {ApiError} from '../api/client';
import {encodeBytes,fileAllowed,createMachineApi,type DeviceSession,type FileOperation,type Machine,type MachineProfile,type ProfileFile} from '../api/machines';
import LoginPage from './LoginPage';
import type {UserResponse} from '../types/auth';
import MachineWorkspace from './MachineWorkspace';
import EmployeeAccountModal from './EmployeeAccountModal';
import PasswordReminder from './PasswordReminder';
import {createUuid} from '../utils/uuid';
const {operate,profileRequest}=createMachineApi(api);
export default function MachinesPage({path,navigate,user,onUserChange}:{user:UserResponse|null;path:string;navigate:(p:string,replace?:boolean)=>void;onUserChange:(user:UserResponse|null)=>void}){
 const device=path.startsWith('/machines/')?path.split('/')[2]:'';
 const [machines,setMachines]=useState<Machine[]>([]),[session,setSession]=useState<DeviceSession|null>(null),[profile,setProfile]=useState<MachineProfile|null>(null),[error,setError]=useState(''),[busy,setBusy]=useState(false),[folder,setFolder]=useState('Desktop'),[selected,setSelected]=useState<ProfileFile|null>(null),[text,setText]=useState(''),[editor,setEditor]=useState(false),[pdf,setPdf]=useState('');
 const [restoring,setRestoring]=useState(true),[restoreError,setRestoreError]=useState(''),[restoreAttempt,setRestoreAttempt]=useState(0);
 const [deleted,setDeleted]=useState<string|null>(null);
 const [accountOpen,setAccountOpen]=useState(false);
 const [passwordChanged,setPasswordChanged]=useState(false);
 const [action,setAction]=useState<string|null>(null),[name,setName]=useState(''),[target,setTarget]=useState('Documents');
 const requestIds=useRef(new Map<string,string>());
 const active=useRef(new AbortController());
 const [machineListStatus,setMachineListStatus]=useState<'loading'|'ready'|'error'>('loading');
 const [listAttempt,setListAttempt]=useState(0);
 useEffect(() => {
  const controller = new AbortController();
  setMachineListStatus('loading');
  api<Machine[]>('/machines', {signal: controller.signal})
   .then(items => {
    if (controller.signal.aborted) return;
    setMachines(items);
    setMachineListStatus('ready');
   })
   .catch(() => { if (!controller.signal.aborted) setMachineListStatus('error'); });
  return () => controller.abort();
 }, [device, listAttempt]);
 function wipe(){setAction(null);setFolder('Desktop');setTarget('Documents');setAccountOpen(false);setDeleted(null);requestIds.current.clear();active.current.abort();active.current=new AbortController();setProfile(null);setSelected(null);setText('');setEditor(false);setPdf('');setSession(null);setError('');setBusy(false)}
 // Khôi phục bằng mã phiên trong tab, không dùng dữ liệu profile được lưu ở client.
 useEffect(()=>{
  wipe();setRestoring(true);setRestoreError('');const c=active.current;
  async function restore(){
   const saved=getToken()?deviceSessions.read(device || undefined):null;
   if(!device && saved){navigate(`/machines/${saved.deviceId}/desktop`,true);return}
   if(!saved){if(!c.signal.aborted){setRestoring(false);if(device&&path.endsWith('/desktop'))navigate(`/machines/${device}/login`)}return}
   try{
    await reportMachinePage(false);
    const p=await profileRequest(device,saved.sessionId,c.signal);
    if(c.signal.aborted)return;
    if(p.deviceId!==device||p.sessionId!==saved.sessionId)throw new Error('Máy chủ trả về phiên không khớp với máy đã chọn.');
    setSession(p);setProfile(p);if(window.location.pathname!==`/machines/${device}/desktop`)navigate(`/machines/${device}/desktop`,true);
   }catch(e){if(!c.signal.aborted){
    if(e instanceof ApiError&&[400,401,403,404].includes(e.status)){clearSession();onUserChange(null);navigate(`/machines/${device}/login`);setError('Phiên đã hết hạn hoặc quyền sử dụng máy đã thay đổi. Vui lòng đăng nhập lại.')}
    else setRestoreError('Chưa thể kiểm tra phiên trên máy. Kiểm tra kết nối và thử lại.');
   }}finally{if(!c.signal.aborted)setRestoring(false)}
  }
  void restore();return()=>{c.abort()};
 },[device,restoreAttempt]);
 useEffect(()=>()=>{if(pdf)URL.revokeObjectURL(pdf)},[pdf]);
 async function enter(user:UserResponse){
  setPasswordChanged(false);
  const c=active.current;let started:DeviceSession|null=null;setBusy(true);setError('');
  try{
   if(c.signal.aborted)return;
   started=await api<DeviceSession>(`/machines/${encodeURIComponent(device)}/sessions`,{method:'POST',body:'{}',signal:c.signal});
   if(c.signal.aborted)return;
   deviceSessions.save(started);
   const p=await operate(device,started.sessionId,{kind:'ensure',folder:'Desktop'},c.signal);
   if(!c.signal.aborted){setSession(started);setProfile(p);onUserChange(user);navigate(`/machines/${device}/desktop`,true)}
  }catch(e){if(!c.signal.aborted){
   const message=(e as Error).message;
   // Giữ token tới khi đóng phiên thất bại khởi tạo xong; không bỏ credential trước logout.
   if(started){try{await endMachineSession(started);clearSession()}
    catch(cleanup){setRestoreError(`Chưa thể đóng phiên vừa tạo. Hãy thử khôi phục phiên. ${(cleanup as Error).message}`)}}
   else clearSession();
   onUserChange(null);setError(message);
  }}finally{if(!c.signal.aborted)setBusy(false)}
 }
 async function leave(){setBusy(true);setError('');try{if(session)await endMachineSession(session);wipe();clearSession();onUserChange(null);navigate(`/machines/${device}/login`,true)}catch(e){setError((e as Error).message);setBusy(false)}}
 async function run(op:FileOperation){if(!session||busy)return;const c=active.current;const key=JSON.stringify([session.sessionId,op]);const requestId=requestIds.current.get(key)??createUuid();setBusy(true);setError('');try{const p=await operate(device,session.sessionId,{...op,requestId},c.signal);requestIds.current.delete(key);if(c.signal.aborted)return;setProfile(p);setAction(null);if(op.kind==='delete')setDeleted(op.fileId??null);if(op.kind==='restore')setDeleted(null);if(op.kind==='read'&&p.contentBase64!=null){const bytes=Uint8Array.from(atob(p.contentBase64),x=>x.charCodeAt(0));if(selected&&/\.(txt|csv)$/i.test(selected.name)){setText(new TextDecoder().decode(bytes));setEditor(true)}else{const blob=new Blob([bytes],{type:selected?.name.toLowerCase().endsWith('.pdf')?'application/pdf':'application/octet-stream'});const url=URL.createObjectURL(blob);if(selected?.name.toLowerCase().endsWith('.pdf'))setPdf(url);else{const a=document.createElement('a');a.href=url;a.download=selected?.name??'document';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000)}}}else if(op.kind!=='list'){setSelected(null);setEditor(false);setText('')}}catch(e){if(!c.signal.aborted){const uncertain=e instanceof TypeError||(e instanceof ApiError&&[408,429,502,503,504].includes(e.status));if(uncertain)requestIds.current.set(key,requestId);else requestIds.current.delete(key);if(e instanceof ApiError&&[401,403].includes(e.status)){wipe();clearSession();onUserChange(null);navigate(`/machines/${device}/login`)}setError((e as Error).message)}}finally{if(!c.signal.aborted)setBusy(false)}}
 async function importFile(file:File){if(!fileAllowed(file.name)||file.size>8*1024*1024){setError('Chỉ nhận tài liệu Office, PDF, TXT, CSV, desktop.ini tối đa 8 MiB.');return}const c=active.current;const bytes=new Uint8Array(await file.arrayBuffer());if(!c.signal.aborted)await run({kind:'create',folder,name:file.name,contentBase64:encodeBytes(bytes)})}
 if(restoring||restoreError)return <main className="session-loading">{restoring?<p role="status">Đang kiểm tra phiên trên máy…</p>:<><Alert type="error" showIcon message={restoreError}/><Button onClick={()=>setRestoreAttempt(v=>v+1)}>Thử lại</Button></>}</main>;
 const machine=machines.find(m=>m.id===device);
 const listError = machineListStatus === 'error' && <Alert type="error" showIcon
  message="Chưa tải được danh sách máy."
  action={<Button onClick={()=>setListAttempt(value=>value+1)}>Thử lại</Button>}/>;
 if(device && !session) return <LoginPage key={device} isAdminRoute={false} machine={{name:machine?.name??device,assetCode:machine?.assetCode}} signedInEmployee={user?.role==='Employee' && getToken() && !deviceSessions.read() ? user : undefined} onLogin={enter} onChooseMachine={()=>navigate('/employee')} notice={<>
  {passwordChanged&&<Alert type="success" showIcon message="Đã đổi mật khẩu" description="Hãy đăng nhập lại bằng mật khẩu mới."/>}
  {error&&<Alert type="error" showIcon message={error}/>}
  {listError}
 </>}/>;
 return <main className={`workstation-shell ${session?'has-session':''}`}><Space className="workstation-header" wrap style={{width:'100%',justifyContent:'space-between'}}><Typography.Title level={2}><DesktopOutlined /> Không gian máy công ty</Typography.Title><Space wrap>{session&&<><Button onClick={()=>navigate('/employee/tickets')}>Yêu cầu hỗ trợ</Button><Button disabled={busy} onClick={()=>void leave()}>Đăng xuất / Đổi máy</Button></>}</Space></Space><Typography.Paragraph className="workstation-description">{isDemoMode ? 'Dữ liệu minh họa trong bộ nhớ trình duyệt.' : 'Desktop/Documents mô phỏng · Tài liệu được lưu trên máy chủ theo từng tài khoản và máy.'}</Typography.Paragraph>{error&&<Alert type="error" showIcon message={error} style={{marginBottom:16}}/>}
 {!device?<section className="machine-chooser"><div className="machine-intro"><span>WORKSTATION / 01</span><h1>Chọn nơi làm việc của bạn.</h1><p>Chọn máy trước khi đăng nhập. Tài khoản nhân viên đang hoạt động có thể dùng mọi máy đang hoạt động. Tài liệu và quota riêng trên từng máy.</p></div>
  {machineListStatus==='loading' && <p role="status">Đang tải danh sách máy…</p>}
  {listError}
  {machineListStatus==='ready' && (machines.length===0 ? <p role="status">Chưa có máy khả dụng.</p> :
   <div className="machine-grid">{machines.map(m=>
    <Card className="machine-card" key={m.id} title={<><DesktopOutlined className="machine-icon" /><span>{m.name}</span></>}>
     <p>{m.assetCode} <Tag color="blue">Mô phỏng</Tag></p>
     <Button type="primary" block onClick={()=>navigate(`/machines/${m.id}/login`)}>Chọn máy →</Button>
    </Card>)}</div>)}
 </section>:session?<>
 <PasswordReminder editing={accountOpen || busy} onChange={()=>setAccountOpen(true)}/><Button style={{marginBottom:16}} disabled={busy} onClick={()=>setAccountOpen(true)}>Thông tin tài khoản</Button>
 {accountOpen&&<EmployeeAccountModal onClose={()=>setAccountOpen(false)} onPasswordChanged={()=>{wipe();clearSession();onUserChange(null);setPasswordChanged(true);navigate(`/machines/${device}/login`)}}/>}
 <MachineWorkspace session={session}><div className="workstation-desktop"><aside className="desktop-shortcuts">{['Desktop','Documents'].map(value=><button key={value} className={folder===value?'active':''} disabled={busy} onClick={()=>{setFolder(value);setSelected(null);setEditor(false);setText('');setPdf('')}}><FolderOpenOutlined /><span>{value}</span></button>)}</aside><section className="file-manager"><div className="file-manager-title"><FolderOpenOutlined /> File Manager <span>{folder}</span></div><Card className="profile-strip"><b>{session.domainId} · {session.deviceName}</b><p>Profile riêng theo tài khoản và thiết bị</p><progress aria-label="Số file đã dùng" max={profile?.quota.maxFiles||1} value={profile?.quota.usedFiles??0}/><p>Quota: {profile?.quota.usedFiles} / {profile?.quota.maxFiles} file · Đang giữ chỗ: {profile?.quota.reservedFiles} · Còn lại: {profile?.quota.remainingFiles}</p>{profile?.quota.overQuota&&<Alert type="warning" message="Đã vượt quota. Xóa file hoặc liên hệ quản trị."/>}</Card>
 <Space wrap style={{margin:'16px 0'}}><Select disabled={busy} aria-label="Thư mục" value={folder} options={[{value:'Desktop'},{value:'Documents'},{value:'RecycleBin',label:'Thùng rác'}]} onChange={v=>{setFolder(v);setSelected(null);setEditor(false);setText('');setPdf('')}}/><Button disabled={busy} onClick={()=>void run({kind:'list',folder})}>Làm mới</Button>{folder!=='RecycleBin'&&<><Button disabled={busy||!profile?.quota.remainingFiles} onClick={()=>{setName('document.txt');setAction('create')}}>Tạo TXT/CSV</Button><label className="file-import"><UploadOutlined /> Nhập tài liệu<input type="file" aria-label="Nhập tài liệu" disabled={busy||!profile?.quota.remainingFiles} accept=".doc,.docx,.xls,.xlsx,.ppt,.pptx,.pdf,.txt,.csv,.ini" onChange={e=>{const f=e.target.files?.[0];e.target.value='';if(f)void importFile(f)}}/></label></>}</Space>
 {deleted&&<Button disabled={busy||!profile?.quota.remainingFiles} onClick={()=>void run({kind:'restore',fileId:deleted,targetFolder:'Documents'})}>Khôi phục file vừa xóa vào Documents</Button>}
 <Table<ProfileFile> scroll={{x:480}} rowKey="fileId" loading={busy} dataSource={profile?.files.filter(f=>f.folder===folder)??[]} rowSelection={{getCheckboxProps:()=>({disabled:busy}),type:'radio',selectedRowKeys:selected?[selected.fileId]:[],onChange:(_,r)=>{setSelected(r[0]);setEditor(false);setText('');setPdf('')}}} columns={[{title:'Tên file',dataIndex:'name',render:(value:string)=><span><FileTextOutlined style={{color:'#286b94',marginRight:8}}/>{value}</span>},{title:'Kích thước (byte)',dataIndex:'sizeBytes'},{title:'Loại',dataIndex:'extension'}]} locale={{emptyText:'Thư mục trống'}}/>
 {selected&&<Space wrap>{folder==='RecycleBin'?<Button disabled={busy||!profile?.quota.remainingFiles} onClick={()=>void run({kind:'restore',fileId:selected.fileId,targetFolder:'Documents'})}>Khôi phục vào Documents</Button>:<><Button disabled={busy} onClick={()=>void run({kind:'read',fileId:selected.fileId})}>Mở / tải xuống</Button>{['rename','move','copy'].map(kind=><Button key={kind} disabled={busy||(kind==='copy'&&!profile?.quota.remainingFiles)} onClick={()=>{setName(selected.name);setAction(kind)}}>{({rename:'Đổi tên',move:'Di chuyển',copy:'Sao chép'})[kind]}</Button>)}<Popconfirm title="Chuyển file vào thùng rác?" onConfirm={()=>run({kind:'delete',fileId:selected.fileId})}><Button danger disabled={busy}>Xóa</Button></Popconfirm></>}</Space>}
 {editor&&selected&&<Card title={selected.name} style={{marginTop:16}}><Input.TextArea aria-label="Nội dung TXT/CSV" rows={12} value={text} onChange={e=>setText(e.target.value)} disabled={busy}/><Button loading={busy} onClick={()=>void run({kind:'write',fileId:selected.fileId,contentBase64:encodeBytes(new TextEncoder().encode(text))})}>Lưu nội dung</Button></Card>}
 {pdf&&<Card title={selected?.name}><a href={pdf} download={selected?.name}>Tải PDF</a><iframe title="Xem PDF" src={pdf} style={{width:'100%',height:600,border:0}}/></Card>}
 </section></div><footer className="workstation-taskbar"><span><FolderOpenOutlined /> File Manager</span><span>{session.deviceName} · {session.domainId}</span><Button disabled={busy} onClick={()=>void leave()}>Đăng xuất</Button></footer></MachineWorkspace></>:null}
 <Modal title={action==='create'?'Tạo tài liệu':action==='rename'?'Đổi tên':action==='move'?'Di chuyển':'Sao chép'} open={!!action} confirmLoading={busy} onCancel={()=>{if(!busy)setAction(null)}} onOk={()=>{if(action==='create'&&!/\.(txt|csv)$/i.test(name)){setError('Tạo tài liệu mới chỉ hỗ trợ TXT/CSV.');return}void run({kind:action!,...(action==='create'?{folder,name,contentBase64:''}:{fileId:selected?.fileId}),...(['rename','copy'].includes(action!)?{newName:name}:{}),...(['move','copy'].includes(action!)?{targetFolder:target}:{})})}}><Input aria-label="Tên file" value={name} onChange={e=>setName(e.target.value)}/>{(action==='move'||action==='copy')&&<Select aria-label="Thư mục đích" value={target} onChange={setTarget} options={['Desktop','Documents'].map(value=>({value}))}/>}</Modal>
 </main>
}
