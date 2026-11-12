import test from 'node:test';
import assert from 'node:assert/strict';
import {createDeviceSessionStore} from '../src/api/device-session.ts';
function storage(){const map=new Map();return {getItem:k=>map.get(k)??null,setItem:(k,v)=>map.set(k,v),removeItem:k=>map.delete(k)}}
test('refresh reads only a reference for the matching device, never caches personal content',()=>{
 const tab=storage(),store=createDeviceSessionStore(tab,'live');
 store.save({deviceId:'a',sessionId:'s',files:['private.txt'],accessToken:'secret'});
 assert.deepEqual(createDeviceSessionStore(tab,'live').read('a'),{deviceId:'a',sessionId:'s'});
 assert.equal(store.read('b'),null);
 assert.deepEqual(JSON.parse(tab.getItem('live')),{deviceId:'a',sessionId:'s'});
 store.clear();assert.equal(store.read('a'),null);
});
test('malformed session references cannot restore a desktop',()=>{
 const tab=storage(),store=createDeviceSessionStore(tab,'live');
 for(const value of ['broken','null','{}','{"deviceId":"a","sessionId":42}','{"deviceId":"a","sessionId":""}']){
  tab.setItem('live',value);assert.equal(store.read('a'),null);
 }
});
test('preview and live references and different tabs stay independent',()=>{
 const tab=storage(),live=createDeviceSessionStore(tab,'live'),preview=createDeviceSessionStore(tab,'preview');
 live.save({deviceId:'a',sessionId:'live-id'});preview.save({deviceId:'a',sessionId:'preview-id'});
 preview.clear();assert.equal(live.read('a').sessionId,'live-id');
 assert.equal(createDeviceSessionStore(storage(),'live').read('a'),null);
});
