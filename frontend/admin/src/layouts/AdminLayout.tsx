import { useState } from 'react';
import type { ReactNode } from 'react';
import { Avatar, Button, Dropdown, Typography } from 'antd';
import { AppstoreOutlined, FileSearchOutlined, LaptopOutlined, LogoutOutlined, MenuFoldOutlined, MenuUnfoldOutlined, TeamOutlined } from '@ant-design/icons';
import type { UserResponse } from '../types/auth';

export type Section = 'overview' | 'assets' | 'users' | 'tickets' | 'quotas' | 'agents';
const { Text } = Typography;
const navItems: { key: Section; label: string; icon: ReactNode; group: string }[] = [
  { key: 'overview', label: 'Tổng quan', icon: <AppstoreOutlined />, group: 'KHÔNG GIAN LÀM VIỆC' },
  { key: 'tickets', label: 'Yêu cầu hỗ trợ', icon: <FileSearchOutlined />, group: 'DỊCH VỤ CNTT' },
  { key: 'agents', label: 'Agent', icon: <LaptopOutlined />, group: 'QUẢN LÝ TÀI SẢN' },
  { key: 'assets', label: 'Tài sản', icon: <LaptopOutlined />, group: 'QUẢN LÝ TÀI SẢN' },
  { key: 'quotas', label: 'Quota profile', icon: <LaptopOutlined />, group: 'QUẢN TRỊ' },
  { key: 'users', label: 'Tài khoản', icon: <TeamOutlined />, group: 'QUẢN TRỊ' },
];

/** Hiển thị khung quản trị gồm điều hướng, thanh tiêu đề và nội dung trang. */
export default function AdminLayout({ user, section, title, onSectionChange, onLogout, children }: {
  user: UserResponse; section: Section; title: string; onSectionChange: (section: Section) => void;
  onLogout: () => void; children: ReactNode;
}) {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileNav, setMobileNav] = useState(false);
  const initials = user.fullName.trim().split(/\s+/).slice(-2).map(part => part[0]).join('').toUpperCase();
  const menu = <div className="profile-menu"><div className="profile-menu-person"><Avatar className="admin-avatar">{initials}</Avatar><span><Text strong>{user.fullName}</Text><Text className="cell-subtitle">{user.email}</Text></span></div><Button type="text" icon={<LogoutOutlined />} onClick={onLogout}>Đăng xuất</Button></div>;

  return <div className={`app-shell ${collapsed ? 'is-collapsed' : ''}`}>
    {mobileNav && <button className="mobile-scrim" aria-label="Đóng điều hướng" onClick={() => setMobileNav(false)} />}
    <aside className={`sidebar ${mobileNav ? 'mobile-open' : ''}`}>
      <div className="sidebar-brand"><span className="brand-mark logo-mark"><img className="brand-logo" src="/brand/stu-logo.png" alt="STU" /></span><span className="brand-name">IT <b>Desk</b><small>QUẢN TRỊ HỆ THỐNG</small></span></div>
      <nav className="side-nav" aria-label="Điều hướng chính">
        {navItems.map((item, index) => <div key={item.key}>
          {(index === 0 || navItems[index - 1].group !== item.group) && <div className="nav-group-label">{item.group}</div>}
          <button className={`nav-link ${section === item.key ? 'active' : ''}`} onClick={() => { onSectionChange(item.key); setMobileNav(false); }} aria-current={section === item.key ? 'page' : undefined}><span className="nav-icon">{item.icon}</span><span>{item.label}</span></button>
        </div>)}
      </nav>
      <div className="sidebar-bottom"><Dropdown dropdownRender={() => menu} trigger={['click']} placement="topLeft"><button className="sidebar-profile"><Avatar className="admin-avatar">{initials}</Avatar><span className="profile-info"><b>{user.fullName}</b><small>Quản trị hệ thống</small></span><span className="profile-dots">···</span></button></Dropdown></div>
    </aside>
    <div className="main-column">
      <header className="topbar">
        <Button type="text" className="mobile-menu-button" aria-label="Mở điều hướng" icon={<MenuUnfoldOutlined />} onClick={() => setMobileNav(true)} />
        <Button type="text" className="collapse-button" aria-label={collapsed ? 'Mở rộng thanh điều hướng' : 'Thu gọn thanh điều hướng'} icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />} onClick={() => setCollapsed(value => !value)} />
        <div className="topbar-context"><span className="breadcrumb-muted">Quản trị</span><span className="breadcrumb-separator">/</span><Text>{title}</Text></div>
        <div className="topbar-right"><Dropdown dropdownRender={() => menu} trigger={['click']}><button className="topbar-user"><Avatar className="admin-avatar" size={32}>{initials}</Avatar><span>{user.fullName}</span></button></Dropdown></div>
      </header>
      {children}
    </div>
  </div>;
}
