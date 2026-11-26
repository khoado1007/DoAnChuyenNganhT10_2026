// API responses are controlled; no application database or Agent files are changed.
const assert = require('node:assert/strict');
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const base = process.env.ITAM_UI_URL || 'http://127.0.0.1:5192';
(async () => {
 const browser = await chromium.launch({channel:'msedge',headless:true});
 try {
  const page = await browser.newPage(); page.setDefaultTimeout(10000);
  const errors=[]; page.on('pageerror', e=>errors.push(e.message));
  const user={id:'employee-a',username:'khoa',fullName:'Nhân viên thử nghiệm',email:'khoa@test.local',role:'Employee',status:'Active'};
  const profile={sessionId:'session-a',deviceId:'machine-a',profileId:'profile-a',domainId:'COMPANY\\khoa',deviceName:'PC-001',files:[{fileId:'file-a',name:'saved.txt',folder:'Desktop',sizeBytes:4,extension:'.txt'}],quota:{maxFiles:10,usedFiles:1,reservedFiles:0,remainingFiles:9,overQuota:false}};
  let profileStatus=200, ensureStatus=200, starts=0, reads=0, logout=0;
  const writes=[];
  await page.route(url=>url.pathname.startsWith('/api/'),async route=>{
   const request=route.request(),path=new URL(request.url()).pathname;
   let data=null,status=200;
   if(path==='/api/auth/login') data={accessToken:'employee-token',user};
   else if(path==='/api/auth/me') data=user;
   else if(path==='/api/me/assets') data=[];
   else if(path==='/api/machines') data=[{id:'machine-a',name:'PC-001',assetCode:'PC-001',mode:'real'},{id:'machine-b',name:'PC-002',assetCode:'PC-002',mode:'simulated'}];
   else if(path==='/api/machines/machine-a/sessions'){ starts++; data=profile; }
   else if(path==='/api/machines/machine-a/files/operations'){
    const op=request.postDataJSON();
    data=profile;
    if(op.kind==='ensure') status=ensureStatus;
    if(op.kind==='read') data={...profile,contentBase64:''};
    if(op.kind==='write'){
     writes.push(op);
     status=writes.length===1?503:200;
    }
   }
   else if(path==='/api/machines/machine-a/profile'){
    reads++; assert.equal(request.headers()['x-device-session'],'session-a');
    assert.equal(request.headers()['authorization'],'Bearer employee-token');
    status=profileStatus;data=profile;
   } else if(path==='/api/machines/machine-a/logout'){assert.equal(request.headers()['authorization'],'Bearer employee-token');logout++;data={ended:true};}
   else if(path.endsWith('/page') || path.endsWith('/password-policy')) data={};
   else if(path.endsWith('/information')) data={software:[]};
   else throw new Error('Unexpected request '+path);
   await route.fulfill({status,contentType:'application/json',body:JSON.stringify({success:status===200,data,message:status===200?'':'Phiên không khả dụng'})});
  });
  await page.goto(base+'/employee');
  await page.getByRole('button',{name:'Chọn máy →',exact:true}).first().click();
  await page.getByLabel('Domain ID',{exact:true}).fill('COMPANY\\khoa');
  await page.getByLabel('Mật khẩu',{exact:true}).fill('test-password');
  await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).click();
  await page.getByText('saved.txt',{exact:true}).waitFor();
  await page.getByRole('row').filter({hasText:'saved.txt'}).getByRole('radio').check();
  await page.getByRole('button',{name:'Mở / tải xuống',exact:true}).click();
  const editor=page.getByLabel('Nội dung TXT/CSV');
  await editor.fill('Bản đang soạn');
  await page.getByRole('button',{name:'Lưu nội dung',exact:true}).click();
  await page.getByText('Phiên không khả dụng',{exact:true}).waitFor();
  assert.equal(await editor.inputValue(),'Bản đang soạn');
  await page.getByRole('button',{name:'Lưu nội dung',exact:true}).click();
  await editor.waitFor({state:'detached'});
  assert.equal(writes.length,2);
  assert.ok(writes[0].requestId);
  assert.deepEqual(writes[1],writes[0],'Retry must reuse the original operation ID and content');
  await page.reload();
  await page.getByText('saved.txt',{exact:true}).waitFor();
  assert.equal(starts,1,'F5 must reuse the existing device session');assert.ok(reads>0);
  profileStatus=503; await page.reload();
  await page.getByRole('button',{name:'Thử lại',exact:true}).waitFor();
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('itdesk.accessToken')),'employee-token');
  assert.equal(await page.getByText('saved.txt',{exact:true}).count(),0,'No desktop before server validation');
  profileStatus=200;await page.getByRole('button',{name:'Thử lại',exact:true}).click();
  await page.getByText('saved.txt',{exact:true}).waitFor();
  // A different machine must not restore machine A's private profile.
  const before=reads;await page.goto(base+'/machines/machine-b/desktop');
  await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).waitFor();
  assert.equal(reads,before);assert.equal(await page.getByText('saved.txt',{exact:true}).count(),0);
  await page.goto(base+'/machines/machine-a/desktop');await page.getByText('saved.txt',{exact:true}).waitFor();
  profileStatus=403;await page.reload();await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).waitFor();
  assert.equal(await page.getByText('saved.txt',{exact:true}).count(),0);
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('itdesk.accessToken')),null);
  profileStatus=200;
  await page.getByLabel('Domain ID',{exact:true}).fill('COMPANY\\khoa');
  await page.getByLabel('Mật khẩu',{exact:true}).fill('test-password');
  await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).click();await page.getByText('saved.txt',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Đăng xuất / Đổi máy',exact:true}).click();
  await page.waitForURL('**/machine-a/login');assert.equal(logout,1);
  await page.goto(base+'/machines/machine-a/desktop');await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).waitFor();
  assert.equal(await page.getByText('saved.txt',{exact:true}).count(),0);
  // Failed profile initialization must close its server session before dropping the token.
  ensureStatus=503;
  await page.getByLabel('Domain ID',{exact:true}).fill('COMPANY\\khoa');
  await page.getByLabel('Mật khẩu',{exact:true}).fill('test-password');
  await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).click();
  await page.getByText('Phiên không khả dụng',{exact:true}).waitFor();
  assert.equal(logout,2);assert.equal(await page.evaluate(()=>sessionStorage.getItem('itdesk.accessToken')),null);
  ensureStatus=200;
  await page.getByLabel('Domain ID',{exact:true}).fill('COMPANY\\khoa');
  await page.getByLabel('Mật khẩu',{exact:true}).fill('test-password');
  await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).click();await page.getByText('saved.txt',{exact:true}).waitFor();
  profileStatus=401;await page.reload();
  await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).waitFor();
  assert.equal(await page.evaluate(()=>sessionStorage.getItem('itdesk.accessToken')),null);
  assert.equal(await page.getByText('saved.txt',{exact:true}).count(),0);
  assert.deepEqual(errors,[]);
  console.log('PASS: portal link, F5 restore, 503 retry, device isolation, revoked session, logout');
 } finally {await browser.close();}
})().catch(error=>{console.error(error);process.exitCode=1});
