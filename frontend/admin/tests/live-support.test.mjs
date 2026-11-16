import assert from 'node:assert/strict';
import {createRequire} from 'node:module';
import test from 'node:test';
const require=createRequire(import.meta.url);
const previous=process.env.ITAM_TEST_PASSWORD;
process.env.ITAM_TEST_PASSWORD='test-only-password';
const {rememberSecret,redactError}=require('./live/support.cjs');
if(previous===undefined) delete process.env.ITAM_TEST_PASSWORD;
else process.env.ITAM_TEST_PASSWORD=previous;

test('live test diagnostics redact passwords, runtime tokens and session IDs',()=>{
 rememberSecret('test-only-runtime-token');
 const message=redactError(new Error('fill test-only-password; token test-only-runtime-token; session 12345678-1234-1234-1234-123456789abc'));
 assert(!message.includes('test-only-password'));
 assert(!message.includes('test-only-runtime-token'));
 assert(!message.includes('12345678-1234-1234-1234-123456789abc'));
 assert(message.includes('fill [REDACTED]'));
});
test('live test diagnostics retain useful errors while redacting JWT-shaped strings',()=>{
 const message=redactError('Expected 409; got 500. eyJheader.payload.signature');
 assert.equal(message,'Expected 409; got 500. [TOKEN]');
});
