import { lazy, Suspense, useEffect, useState } from 'react';
import { Alert, Button, Card, Statistic } from 'antd';
import AdminLayout, { type Section } from '../layouts/AdminLayout';
import type { UserResponse } from '../types/auth';
import { api } from '../api/auth';const AssetsPage = lazy(() => import('./AssetsPage'));
const UsersPage = lazy(() => import('./UsersPage'));
const AgentsPage = lazy(() => import('./AgentsPage'));
const QuotasPage = lazy(() => import('./QuotasPage'));
const AuditLogPage = lazy(() => import('./AuditLogPage'));
const FilesAdminPage = lazy(() => import('./FilesAdminPage'));
const TicketsPage = lazy(() => import('./TicketsPage'));
const titles: Record<Section, string> = {overview:'Tổng quan',assets:'Tài sản',users:'Tài khoản',agents:'Agent',tickets:'Yêu cầu hỗ trợ',quotas:'Quota tài khoản','audit-log':'Nhật ký thao tác','files-admin':'Metadata file'};
function Dashboard({navigate}:{navigate:(section:Section)=>void}) {
 const [data,setData]=useState<Record<string,number>|null>(null);
 const [error,setError]=useState(''); const [revision,setRevision]=useState(0);
 useEffect(()=>{const c=new AbortController();setError('');setData(null);api<Record<string,number>>('/dashboard',{signal:c.signal}).then(d=>{if(!c.signal.aborted)setData(d)}).catch(e=>{if(!c.signal.aborted)setError(e.message)});return()=>c.abort()},[revision]);
 return <main className="page-content"><div className="page-heading"><div><span className="eyebrow">QUẢN TRỊ HỆ THỐNG</span><h1>Tổng quan</h1><p className="page-description">Thiết bị, tài khoản và không gian làm việc nội bộ.</p></div><Button onClick={()=>setRevision(v=>v+1)}>Làm mới số liệu</Button></div>{error&&<Alert type="error" message={error}/>}<div className="metric-grid">{Object.entries({users:'Tài khoản',assets:'Thiết bị',assignedAssets:'Đã cấp phát',profiles:'Profile'}).map(([key,label])=><Card key={key} loading={!data&&!error}><Statistic title={label} value={data?.[key]??'—'}/></Card>)}</div><div className="dashboard-workflows"><Card title="Quản lý không gian làm việc"><p>Quản lý tài khoản và cấu hình máy mô phỏng. Mỗi nhân viên có hồ sơ riêng trên từng máy và một quota chung cho tài khoản.</p><div className="dashboard-links">{([{section:'users',title:'01 · Tài khoản & Domain ID',description:'Tạo nhân viên, phân vai trò và quản lý tài khoản.'},{section:'assets',title:'02 · Thiết bị & cấu hình mô phỏng',description:'Khai báo cấu hình và ghi nhận trách nhiệm bàn giao.'},{section:'quotas',title:'03 · Quota theo tài khoản',description:'Theo dõi số file và điều chỉnh giới hạn của từng tài khoản.'}] as const).map(item=><button key={item.section} onClick={()=>navigate(item.section)}><strong>{item.title}<span>→</span></strong><small>{item.description}</small></button>)}</div></Card><Card title="Nguyên tắc vận hành"><ul className="dashboard-principles"><li><b>Profile riêng biệt</b><p>Tài liệu không đồng bộ giữa các máy.</p></li><li><b>Quota theo số lượng file</b><p>Mặc định 10 file cho mỗi nhân viên, cộng tất cả máy.</p></li><li><b>Nhân viên đang hoạt động</b><p>Được đăng nhập mọi máy đang hoạt động; không cần cấp quyền từng máy.</p></li></ul></Card></div></main>
}
export default function AdminPreviewPage({user,onLogout,section,setSection}:{user:UserResponse;onLogout:()=>void;section:Section;setSection:(section:Section)=>void}) {
 return <AdminLayout user={user} onLogout={onLogout} section={section} title={titles[section]} onSectionChange={setSection}><Suspense fallback={<p role="status">Đang tải…</p>}>{section==='assets'?<AssetsPage/>:section==='users'?<UsersPage/>:section==='agents'?<AgentsPage/>:section==='quotas'?<QuotasPage/>:section==='audit-log'?<AuditLogPage/>:section==='files-admin'?<FilesAdminPage/>:section==='tickets'?<TicketsPage/>:<Dashboard navigate={setSection}/>}</Suspense></AdminLayout>
}
