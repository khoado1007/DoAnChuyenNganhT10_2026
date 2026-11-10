import { lazy, Suspense, useEffect, useState } from 'react';
import { Alert, Button, Card, Statistic } from 'antd';
import AdminLayout, { type Section } from '../layouts/AdminLayout';
import type { UserResponse } from '../types/auth';
import { api } from '../api/auth';
const AssetsPage = lazy(() => import('./AssetsPage'));
const UsersPage = lazy(() => import('./UsersPage'));
const AgentsPage = lazy(() => import('./AgentsPage'));
const QuotasPage = lazy(() => import('./QuotasPage'));
const titles: Record<Section, string> = {overview:'Tổng quan',assets:'Tài sản',users:'Tài khoản',agents:'Agent',tickets:'Yêu cầu hỗ trợ',quotas:'Quota profile'};
function Dashboard({navigate}:{navigate:(section:Section)=>void}) {
 const [data,setData]=useState<Record<string,number>|null>(null);
 const [error,setError]=useState(''); const [revision,setRevision]=useState(0);
 useEffect(()=>{const c=new AbortController();setError('');setData(null);api<Record<string,number>>('/dashboard',{signal:c.signal}).then(d=>{if(!c.signal.aborted)setData(d)}).catch(e=>{if(!c.signal.aborted)setError(e.message)});return()=>c.abort()},[revision]);
 return <main className="page-content"><div className="page-heading"><div><span className="eyebrow">QUẢN TRỊ HỆ THỐNG</span><h1>Tổng quan</h1><p className="page-description">Thiết bị, tài khoản và không gian làm việc nội bộ.</p></div><Button onClick={()=>setRevision(v=>v+1)}>Làm mới số liệu</Button></div>{error&&<Alert type="error" message={error}/>}<div className="metric-grid">{Object.entries({users:'Tài khoản',assets:'Thiết bị',assignedAssets:'Đã cấp phát',agents:'Agent đăng ký',profiles:'Profile'}).map(([key,label])=><Card key={key} loading={!data&&!error}><Statistic title={label} value={data?.[key]??'—'}/></Card>)}</div><div className="dashboard-workflows"><Card title="Quản lý không gian làm việc"><p>Thiết lập từ tài khoản đến quyền sử dụng máy. Mỗi nhân viên có profile và quota riêng trên từng thiết bị.</p><div className="dashboard-links">{([{section:'users',title:'01 · Tài khoản & Domain ID',description:'Tạo nhân viên, phân vai trò và quản lý tài khoản.'},{section:'assets',title:'02 · Thiết bị & quyền truy cập',description:'Cấp phát trách nhiệm và cấp quyền đăng nhập độc lập.'},{section:'quotas',title:'03 · Quota theo profile',description:'Theo dõi số file và điều chỉnh giới hạn từng cặp người–máy.'}] as const).map(item=><button key={item.section} onClick={()=>navigate(item.section)}><strong>{item.title}<span>→</span></strong><small>{item.description}</small></button>)}</div></Card><Card title="Nguyên tắc vận hành"><ul className="dashboard-principles"><li><b>Profile riêng biệt</b><p>Tài liệu không đồng bộ giữa các máy.</p></li><li><b>Quota theo số lượng file</b><p>Mặc định 10 file cho mỗi nhân viên trên mỗi máy.</p></li><li><b>Quyền truy cập độc lập</b><p>Cấp phát tài sản không tự động cấp quyền đăng nhập.</p></li></ul><Button onClick={()=>navigate('agents')}>Quản lý đăng ký Agent</Button></Card></div></main>
}
export default function AdminPreviewPage({user,onLogout,section,setSection}:{user:UserResponse;onLogout:()=>void;section:Section;setSection:(section:Section)=>void}) {
 return <AdminLayout user={user} onLogout={onLogout} section={section} title={titles[section]} onSectionChange={setSection}><Suspense fallback={<p role="status">Đang tải…</p>}>{section==='assets'?<AssetsPage/>:section==='users'?<UsersPage/>:section==='agents'?<AgentsPage/>:section==='quotas'?<QuotasPage/>:section==='tickets'?<main className="page-content"><h1>Yêu cầu hỗ trợ</h1><Alert type="info" message="Phân hệ ticket chưa triển khai. Chưa có thao tác tạo hoặc cập nhật yêu cầu."/></main>:<Dashboard navigate={setSection}/>}</Suspense></AdminLayout>
}
