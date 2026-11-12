import React from 'react';
import ReactDOM from 'react-dom/client';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import App from './App';
import PageBoundary from './PageBoundary';
import '@fontsource/be-vietnam-pro/400.css';
import '@fontsource/be-vietnam-pro/500.css';
import '@fontsource/be-vietnam-pro/600.css';
import '@fontsource/be-vietnam-pro/700.css';
import './styles.css';
import DemoBanner from './demo/DemoBanner';
import { isDemoMode } from './demo/mode';

if (isDemoMode) document.body.classList.add('demo-mode');

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider
      locale={viVN}
      theme={{
        token: {
          colorPrimary: '#0047AB',
          colorInfo: '#1F6FEB',
          colorSuccess: '#18794e',
          colorWarning: '#9a6700',
          colorError: '#b42318',
          colorText: '#1B2A4A',
          colorTextSecondary: '#627181',
          colorBorder: '#DDE3E8',
          colorBgLayout: '#FFFFFF',
          colorBgContainer: '#ffffff',
          borderRadius: 8,
          controlHeight: 40,
          fontFamily: '"Be Vietnam Pro", "Segoe UI", Arial, sans-serif',
          fontSize: 14,
        },
        components: {
          Button: { primaryShadow: 'none', defaultShadow: 'none' },
          Table: { headerBg: '#f7f9fb', rowHoverBg: '#f7f9fb' },
          Menu: { itemSelectedBg: '#EAF3FC', itemSelectedColor: '#0047AB' },
        },
      }}
    >
      <DemoBanner /><PageBoundary><App /></PageBoundary>
    </ConfigProvider>
  </React.StrictMode>,
);

