const { rememberSecret, redactError } = require('./support.cjs');
// Run via agent/profile_e2e.py: isolated test database, real HTTP and Python worker.
const assert=require('node:assert/strict'),fs=require('node:fs'),path=require('node:path');
const {chromium}=require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const device=process.env.ITAM_DEVICE,password=process.env.ITAM_TEST_PASSWORD,dir=process.env.ITAM_ARTIFACTS;
const ui=process.env.ITAM_UI_URL,apiBase=process.env.ITAM_API_URL;
assert.match(process.env.ITAM_TEST_RUN || '', /^[0-9a-f]{32}$/, 'Use the isolated Python runner');
for (const value of [process.env.ITAM_UI_URL, process.env.ITAM_API_URL]) { const u=new URL(value); assert.equal(u.hostname,'127.0.0.1'); assert.equal(u.protocol,'http:'); }
(async()=>{
 const browser=await chromium.launch({channel:'msedge',headless:true});const page=await browser.newPage({viewport:{width:1365,height:900}});page.setDefaultTimeout(20000);
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 try{
  await page.goto(ui+'/machines/'+device+'/login?demo=0');
  await page.getByLabel('Domain ID',{exact:true}).fill('COMPANY\\live_employee');await page.getByLabel('Mật khẩu',{exact:true}).fill(password);
  await page.getByRole('button',{name:'Đăng nhập máy',exact:true}).click();await page.getByRole('button',{name:'Tạo TXT/CSV',exact:true}).waitFor();
  const auth=await page.evaluate(()=>({token:sessionStorage.getItem('itdesk.accessToken'),session:JSON.parse(sessionStorage.getItem('itdesk.deviceSession')).sessionId}));
  rememberSecret(auth.token);
  async function api(body){const r=await fetch(apiBase+'/api/machines/'+device+'/files/operations',{method:'POST',headers:{'Content-Type':'application/json',Authorization:'Bearer '+auth.token,'X-Device-Session':auth.session},body:JSON.stringify({requestId:crypto.randomUUID(),...body})});const j=await r.json();assert.equal(r.status,200,j.message);return j.data;}
  async function snapshot(){return api({kind:'list',folder:'Desktop'});}
  async function select(name){await page.getByRole('row').filter({has:page.getByText(name,{exact:true})}).getByRole('radio').check();}
  async function confirm(){await page.getByRole('dialog').getByRole('button',{name:/^OK$|^Đồng ý$/}).click();await page.getByRole('dialog').waitFor({state:'hidden'});}
  await api({kind:'create',folder:'Desktop',name:'original.txt',contentBase64:Buffer.from('Extended UTF-8 — nội dung').toString('base64')});
  await page.getByRole('button',{name:'Làm mới từ Agent',exact:true}).click();await select('original.txt');
  await page.getByRole('button',{name:'Đổi tên',exact:true}).click();await page.getByLabel('Tên file',{exact:true}).fill('renamed.txt');await confirm();
  await page.getByText('renamed.txt',{exact:true}).waitFor();assert.equal(await page.getByText('original.txt',{exact:true}).count(),0);
  console.log('PASS 1: rename through UI');
  await select('renamed.txt');await page.getByRole('button',{name:'Sao chép',exact:true}).click();await page.getByLabel('Tên file',{exact:true}).fill('copy.txt');await confirm();
  let snap=await snapshot();assert.equal(snap.quota.usedFiles,2);assert(snap.files.some(f=>f.name==='copy.txt'&&f.folder==='Documents'));
  console.log('PASS 2: copy to Documents increases quota by one');
  await select('renamed.txt');await page.getByRole('button',{name:'Di chuyển',exact:true}).click();await confirm();
  snap=await snapshot();assert.equal(snap.quota.usedFiles,2);assert(snap.files.some(f=>f.name==='renamed.txt'&&f.folder==='Documents'));assert(!snap.files.some(f=>f.name==='renamed.txt'&&f.folder==='Desktop'));
  console.log('PASS 3: move changes folder without increasing quota');
  await page.locator('.desktop-shortcuts button').filter({hasText:'Documents'}).click();await select('copy.txt');await page.getByRole('button',{name:'Xóa',exact:true}).click();await page.locator('.ant-popconfirm-buttons .ant-btn-primary').click();
  await page.getByText('copy.txt',{exact:true}).waitFor({state:'hidden'});
  snap=await snapshot();assert.equal(snap.quota.usedFiles,1);
  console.log('PASS 4: delete moves to recycle and releases quota');
  await page.getByRole('button',{name:'Khôi phục file vừa xóa vào Documents',exact:true}).click();await page.getByText('copy.txt',{exact:true}).waitFor();
  snap=await snapshot();assert.equal(snap.quota.usedFiles,2);
  console.log('PASS 5: restore returns file and quota');
  await page.locator('.desktop-shortcuts button').filter({hasText:'Desktop'}).click();
  await page.getByLabel('Nhập tài liệu',{exact:true}).setInputFiles({name:'import.csv',mimeType:'text/csv',buffer:Buffer.from('name,value\nalpha,42\n')});
  await page.getByText('import.csv',{exact:true}).waitFor();snap=await snapshot();const imported=snap.files.find(f=>f.name==='import.csv');
  assert.equal(Buffer.from((await api({kind:'read',fileId:imported.fileId})).contentBase64,'base64').toString(),'name,value\nalpha,42\n');
  console.log('PASS 6: import via browser preserves bytes');
  const mutation={kind:'create',folder:'Desktop',name:'idempotent.txt',contentBase64:Buffer.from('once').toString('base64'),requestId:crypto.randomUUID()};
  const first=await api(mutation);const originalId=first.files.find(f=>f.name==='idempotent.txt').fileId;
  fs.writeFileSync(path.join(dir,'restart.request'),'restart');
  const deadline=Date.now()+20000;while(!fs.existsSync(path.join(dir,'restart.done'))){assert(Date.now()<deadline,'Restart acknowledgement timeout');await new Promise(r=>setTimeout(r,100));}
  await page.reload();await page.getByText('import.csv',{exact:true}).waitFor();
  assert(await page.evaluate(()=>JSON.parse(sessionStorage.getItem('itdesk.deviceSession')).sessionId)===auth.session,'Session changed unexpectedly');
  snap=await snapshot();assert.equal(snap.quota.usedFiles,4);assert.equal(snap.quota.reservedFiles,0);
  for(const name of ['renamed.txt','copy.txt']){const file=snap.files.find(f=>f.name===name);assert(file);assert.equal(Buffer.from((await api({kind:'read',fileId:file.fileId})).contentBase64,'base64').toString(),'Extended UTF-8 — nội dung');}
  console.log('PASS 7: process restart reuses state/root, preserves session and content');
  const replay=await api(mutation);assert.equal(replay.files.filter(f=>f.name==='idempotent.txt').length,1);assert.equal(replay.files.find(f=>f.name==='idempotent.txt').fileId,originalId);assert.equal(replay.quota.usedFiles,4);
  console.log('PASS 8: replay confirmed mutation after restart does not duplicate file/quota');
  for(const name of ['sample.pdf','sample.docx']){
   await page.getByLabel('Nhập tài liệu',{exact:true}).setInputFiles(path.join(dir,name));
   await page.getByText(name,{exact:true}).waitFor();
   snap=await snapshot();const file=snap.files.find(f=>f.name===name);assert(file);
   assert.deepEqual(Buffer.from((await api({kind:'read',fileId:file.fileId})).contentBase64,'base64'),fs.readFileSync(path.join(dir,name)));
   await select(name);
   if(name.endsWith('.docx')){
    const downloadPromise=page.waitForEvent('download');
    await page.getByRole('button',{name:'Mở / tải xuống',exact:true}).click();
    const download=await downloadPromise;assert.equal(download.suggestedFilename(),name);
    assert.deepEqual(fs.readFileSync(await download.path()),fs.readFileSync(path.join(dir,name)));
   }else{
    await page.getByRole('button',{name:'Mở / tải xuống',exact:true}).click();
    await page.locator('iframe[title="Xem PDF"]').waitFor();
    assert.match(await page.locator('iframe[title="Xem PDF"]').getAttribute('src'),/^blob:/);
   }
  }
  console.log('PASS 9: PDF/DOCX import byte fidelity, PDF viewer opens and DOCX download matches');
  // The same identity must survive restoring to a different folder and restoring back.
  for(const targetFolder of ['Documents','Desktop']){
   await api({kind:'delete',fileId:originalId});
   const restored=await api({kind:'restore',fileId:originalId,targetFolder});
   assert(restored.files.some(f=>f.fileId===originalId&&f.folder===targetFolder&&f.name==='idempotent.txt'));
   assert.equal(Buffer.from((await api({kind:'read',fileId:originalId})).contentBase64,'base64').toString(),'once');
  }
  console.log('PASS 10: cross-folder restore preserves file identity and content');
  fs.writeFileSync(path.join(dir,'api-restart.request'),'restart');
  const apiDeadline=Date.now()+60000;
  while(!fs.existsSync(path.join(dir,'api-restart.done'))){assert(Date.now()<apiDeadline,'API restart acknowledgement timeout');await new Promise(r=>setTimeout(r,100));}
  await page.reload();await page.getByText('sample.pdf',{exact:true}).waitFor();
  assert(await page.evaluate(()=>JSON.parse(sessionStorage.getItem('itdesk.deviceSession')).sessionId)===auth.session,'Session changed unexpectedly');
  snap=await snapshot();assert.equal(snap.quota.usedFiles,6);assert.equal(snap.quota.reservedFiles,0);
  assert.equal(Buffer.from((await api({kind:'read',fileId:originalId})).contentBase64,'base64').toString(),'once');
  assert.equal((await api(mutation)).quota.usedFiles,6);
  console.log('PASS 11: API restart preserves authenticated device session, metadata and confirmed replay');
  assert.deepEqual(errors,[]);await page.screenshot({path:path.join(dir,'extended-live.png'),fullPage:true});
 }catch(e){await page.screenshot({path:path.join(dir,'extended-failure.png'),fullPage:true});throw e;}finally{await browser.close();}
})().catch(e=>{console.error(redactError(e));process.exitCode=1;});

