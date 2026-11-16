const { rememberSecret, redactError } = require('./support.cjs');
// Run via agent/profile_e2e.py: isolated test database, real HTTP and Python worker.
const assert=require('node:assert/strict');
const path=require('node:path');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const ui=process.env.ITAM_UI_URL,api=process.env.ITAM_API_URL+'/api';
const device=process.env.ITAM_DEVICE,password=process.env.ITAM_TEST_PASSWORD;
async function call(route,{method='GET',body,token,session}={}){
 const response=await fetch(api+route,{method,headers:{'Content-Type':'application/json',...(token?{Authorization:'Bearer '+token}:{}),...(session?{'X-Device-Session':session}:{})},body:body===undefined?undefined:JSON.stringify(body)});
 return {status:response.status,body:await response.json()};
}
assert.match(process.env.ITAM_TEST_RUN || '', /^[0-9a-f]{32}$/, 'Use the isolated Python runner');
for (const value of [process.env.ITAM_UI_URL, process.env.ITAM_API_URL]) { const u=new URL(value); assert.equal(u.hostname,'127.0.0.1'); assert.equal(u.protocol,'http:'); }
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});
 const page=await browser.newPage({viewport:{width:1365,height:900}});page.setDefaultTimeout(25000);
 const errors=[];page.on('pageerror',e=>errors.push(e.message));let starts=0;
 page.on('request',r=>{if(r.method()==='POST'&&r.url().endsWith('/sessions'))starts++;});
 try{
  await page.goto(ui+'/employee?demo=0');
  await page.getByLabel('Tên đăng nhập',{exact:true}).fill('live_employee');
  await page.getByLabel('Mật khẩu',{exact:true}).fill(password);
  await page.getByRole('button',{name:/Đăng nhập$/}).click();
  await page.getByRole('link',{name:'Chọn máy làm việc',exact:true}).click();
  await page.getByRole('button',{name:'Chọn máy →',exact:true}).click();
  async function enter(){
   await page.getByLabel('Domain ID',{exact:true}).fill('COMPANY\\live_employee');
   await page.getByLabel('Mật khẩu',{exact:true}).fill(password);
   await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).click();
   await page.getByRole('button',{name:'Tạo TXT/CSV',exact:true}).waitFor();
  }
  await enter();
  console.log('PASS 1: portal -> select machine -> domain login -> real Agent ensure');
  await page.getByRole('button',{name:'Tạo TXT/CSV',exact:true}).click();
  await page.getByLabel('Tên file',{exact:true}).fill('live-note.txt');
  await page.getByRole('dialog').getByRole('button',{name:/^OK$|^Đồng ý$/}).click();
  await page.getByText('live-note.txt',{exact:true}).waitFor();
  await page.getByRole('row').filter({hasText:'live-note.txt'}).getByRole('radio').check();
  await page.getByRole('button',{name:'Mở / tải xuống',exact:true}).click();
  await page.getByLabel('Nội dung TXT/CSV').fill('Saved through real Python Agent — kiểm thử');
  await page.getByRole('button',{name:'Lưu nội dung',exact:true}).click();
  await page.getByLabel('Nội dung TXT/CSV').waitFor({state:'hidden'});
  console.log('PASS 2: create/read/write via browser and real Python worker');
  const before=await page.evaluate(()=>({token:sessionStorage.getItem('itdesk.accessToken'),ref:JSON.parse(sessionStorage.getItem('itdesk.deviceSession'))}));
  await page.reload();await page.getByText('live-note.txt',{exact:true}).waitFor();
  assert.equal(starts,1,'F5 created a new device session');
  rememberSecret(before.token);
  const after=await page.evaluate(()=>JSON.parse(sessionStorage.getItem('itdesk.deviceSession')));assert(after.deviceId===before.ref.deviceId && after.sessionId===before.ref.sessionId,'F5 changed the device session');
  await page.getByRole('row').filter({hasText:'live-note.txt'}).getByRole('radio').check();
  await page.getByRole('button',{name:'Mở / tải xuống',exact:true}).click();
  await page.getByLabel('Nội dung TXT/CSV').waitFor();
  assert.equal(await page.getByLabel('Nội dung TXT/CSV').inputValue(),'Saved through real Python Agent — kiểm thử');
  console.log('PASS 3: F5 restores same session and saved content');
  const route='/machines/'+device,token=before.token,session=before.ref.sessionId;
  const snapshot=await call(route+'/profile',{token,session});assert.equal(snapshot.status,200);
  const quota=await call('/profiles/'+snapshot.body.data.profileId+'/quota',{method:'PUT',body:{maxFiles:2},token:process.env.ITAM_ADMIN_TOKEN});assert.equal(quota.status,200);
  const concurrent=await Promise.all(['quota-a.txt','quota-b.txt'].map(name=>call(route+'/files/operations',{method:'POST',token,session,body:{kind:'create',folder:'Desktop',name,requestId:crypto.randomUUID(),contentBase64:''}})));
  assert.equal(concurrent.filter(x=>x.status===200).length,1,JSON.stringify(concurrent.map(x=>({status:x.status,message:x.body.message}))));
  assert.equal(concurrent.filter(x=>x.status===409).length,1,'Expected quota conflict');
  const full=await call(route+'/profile',{token,session});assert.equal(full.body.data.quota.usedFiles,2);assert.equal(full.body.data.quota.reservedFiles,0);
  console.log('PASS 4: concurrent creates obey maxFiles=2, no leftover reservation');
  const other=await call('/auth/login',{method:'POST',body:{username:'live_other',password}});assert.equal(other.status,200);
  assert.equal((await call(route+'/profile',{token:other.body.data.accessToken,session})).status,403);
  assert.equal((await call(route+'/sessions',{method:'POST',body:{},token:other.body.data.accessToken})).status,403);
  console.log('PASS 5: another employee cannot read profile or start unauthorized session');
  await page.screenshot({path:path.join(process.env.ITAM_ARTIFACTS,'desktop-live.png'),fullPage:true});
  await page.getByRole('button',{name:'Đăng xuất / Đổi máy',exact:true}).click();
  await page.getByRole('button',{name:'Chọn máy →',exact:true}).waitFor();
  assert(await page.evaluate(()=>sessionStorage.getItem('itdesk.accessToken'))===null,'Token was not cleared');
  assert.equal((await call(route+'/profile',{token,session})).status,403);
  console.log('PASS 6: logout clears browser token and server rejects ended session');
  await page.getByRole('button',{name:'Chọn máy →',exact:true}).click();await enter();
  await page.getByText('live-note.txt',{exact:true}).waitFor();
  const me=await call('/auth/me',{token});assert.equal(me.status,200);
  assert.equal((await call('/assets/'+device+'/access/'+me.body.data.id,{method:'DELETE',token:process.env.ITAM_ADMIN_TOKEN})).status,200);
  await page.reload();await page.getByLabel('Domain ID',{exact:true}).waitFor();
  assert.equal(await page.getByText('live-note.txt',{exact:true}).count(),0);
  assert(await page.evaluate(()=>sessionStorage.getItem('itdesk.deviceSession'))===null,'Device session was not cleared');
  assert.deepEqual(errors,[]);
  console.log('PASS 7: revoked permission + F5 does not reveal profile; no JS page errors');
 }catch(e){await page.screenshot({path:path.join(process.env.ITAM_ARTIFACTS,'failure.png'),fullPage:true});throw e;}
 finally{await browser.close();}
})().catch(e=>{console.error(redactError(e));process.exitCode=1});

