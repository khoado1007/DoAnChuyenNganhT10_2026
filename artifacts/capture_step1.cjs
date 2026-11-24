const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'D:/DoAnChuyenNganhT10_2026/frontend/admin/node_modules/playwright');
const path = require('node:path');
const fs = require('node:fs');

const OUT_DIR = 'C:/Users/DANGKHOA/Desktop/Screenshots_BaoCao';
const BASE_URL = 'http://127.0.0.1:5173';

(async () => {
  if (!fs.existsSync(OUT_DIR)) fs.mkdirSync(OUT_DIR, { recursive: true });
  const browser = await chromium.launch({ channel: 'msedge', headless: true });
  const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const page = await context.newPage();

  console.log('1. Đăng nhập Admin và chụp Hình 3-8 (Dashboard)...');
  await page.goto(`${BASE_URL}/admin?demo=0`);
  await page.waitForLoadState('networkidle');

  // Điền form đăng nhập Admin
  await page.getByLabel('Tên đăng nhập').fill('admin.demo');
  await page.getByLabel('Mật khẩu', { exact: true }).fill('12345Az@');
  await page.getByRole('button', { name: 'Đăng nhập', exact: true }).click();

  // Chờ Dashboard hiển thị
  await page.waitForURL('**/admin**');
  await page.waitForTimeout(3000);
  await page.screenshot({ path: path.join(OUT_DIR, 'Hinh_3-8_Admin_Dashboard.png'), fullPage: false });
  console.log('Saved Hinh_3-8_Admin_Dashboard.png');

  // Chụp Hình 3-9 (Hạn mức lưu trữ / Quota)
  console.log('2. Chuyển sang trang Quota và chụp Hình 3-9...');
  await page.goto(`${BASE_URL}/admin/quotas?demo=0`);
  await page.waitForLoadState('networkidle');
  await page.waitForTimeout(3000);
  await page.screenshot({ path: path.join(OUT_DIR, 'Hinh_3-9_Cap_quyen_va_Quota.png'), fullPage: false });
  console.log('Saved Hinh_3-9_Cap_quyen_va_Quota.png');

  await browser.close();
  console.log('Done test 1');
})().catch(err => {
  console.error('Error:', err);
  process.exit(1);
});
