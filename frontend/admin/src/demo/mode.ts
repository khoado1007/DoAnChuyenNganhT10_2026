const key = 'itdesk.previewMode';
const browser = typeof window !== 'undefined';
if (browser) {
  const option = new URLSearchParams(window.location.search).get('demo');
  if (option === '1') sessionStorage.setItem(key, '1');
  if (option === '0') sessionStorage.removeItem(key);
}
export const isDemoMode = browser && sessionStorage.getItem(key) === '1';
