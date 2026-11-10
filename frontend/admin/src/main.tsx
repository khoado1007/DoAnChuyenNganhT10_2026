import React from 'react';
import ReactDOM from 'react-dom/client';
import { ConfigProvider } from 'antd';
import viVN from 'antd/locale/vi_VN';
import App from './App';
import PageBoundary from './PageBoundary';
import '@fontsource/noto-serif/400.css';
import '@fontsource/noto-serif/600.css';
import '@fontsource/noto-serif/700.css';
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
          colorPrimary: '#17324d',
          colorInfo: '#245ea8',
          colorSuccess: '#18794e',
          colorWarning: '#9a6700',
          colorError: '#b42318',
          colorText: '#202b36',
          colorTextSecondary: '#627181',
          colorBorder: '#d7dee5',
          colorBgLayout: '#f4f6f8',
          colorBgContainer: '#ffffff',
          borderRadius: 8,
          controlHeight: 40,
          fontFamily: '"Segoe UI", Arial, sans-serif',
          fontSize: 14,
        },
        components: {
          Button: { primaryShadow: 'none', defaultShadow: 'none' },
          Table: { headerBg: '#f7f9fb', rowHoverBg: '#f7f9fb' },
          Menu: { itemSelectedBg: '#eef2f5', itemSelectedColor: '#17324d' },
        },
      }}
    >
      <DemoBanner /><PageBoundary><App /></PageBoundary>
    </ConfigProvider>
  </React.StrictMode>,
);

