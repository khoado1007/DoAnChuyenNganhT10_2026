import test from 'node:test';
import assert from 'node:assert/strict';
import {createApiClient,ApiError} from '../src/api/client.ts';
import {createMachineApi,fileAllowed,encodeBytes} from '../src/api/machines.ts';
test('device requests bind JWT and device session and preserve operation id on retry',async()=>{
 let calls=0;const controller=new AbortController();const requestId=crypto.randomUUID();
 const request=createApiClient('/api',()=> 'jwt',()=>{},async(url,init)=>{
  calls++;assert.equal(init.signal,controller.signal);assert.equal(new Headers(init.headers).get('X-Device-Session'),'session-a');assert.equal(new Headers(init.headers).get('Authorization'),'Bearer jwt');
  if(init.method==='POST'){assert.equal(url,'/api/machines/machine-a/files/operations');const body=JSON.parse(init.body);assert.equal(body.kind,'create');assert.equal(body.requestId,requestId);assert.equal(body.ownerId,undefined);return Response.json({success:false,message:'Pending result'},{status:503})}
  assert.equal(url,'/api/machines/machine-a/profile');return Response.json({success:true,data:{profileId:'profile-a'}});
 });
 const machines=createMachineApi(request);assert.deepEqual(await machines.profileRequest('machine-a','session-a',controller.signal),{profileId:'profile-a'});
 const operation={kind:'create',name:'hello.txt',folder:'Desktop',requestId};
 for(let attempt=0;attempt<2;attempt++)await assert.rejects(machines.operate('machine-a','session-a',operation,controller.signal),e=>e instanceof ApiError&&e.status===503);
 assert.equal(calls,3);
});
test('document allowlist rejects executables and encodes Unicode bytes losslessly',()=>{
 for(const name of ['a.doc','a.docx','a.xlsx','a.pptx','a.pdf','a.csv','a.TXT','desktop.ini','other.ini'])assert.equal(fileAllowed(name),true);
 for(const name of ['a.exe','a.txt.exe','a.js'])assert.equal(fileAllowed(name),false);
 const bytes=new TextEncoder().encode('Tài liệu tiếng Việt');assert.deepEqual(Uint8Array.from(atob(encodeBytes(bytes)),x=>x.charCodeAt(0)),bytes);
});
