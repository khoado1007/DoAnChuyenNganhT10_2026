import test from 'node:test';
import assert from 'node:assert/strict';
import { createUuid } from '../src/utils/uuid.ts';

test('createUuid uses randomUUID when the browser provides it', () => {
  assert.equal(createUuid({ randomUUID: () => 'native-uuid' }), 'native-uuid');
});

test('createUuid falls back to secure random bytes and formats a UUID v4', () => {
  const uuid = createUuid({
    getRandomValues(bytes) {
      bytes.set(Array.from({ length: 16 }, (_, index) => index));
      return bytes;
    },
  });

  assert.equal(uuid, '00010203-0405-4607-8809-0a0b0c0d0e0f');
});

test('createUuid fails clearly when secure randomness is unavailable', () => {
  assert.throws(() => createUuid({}), /bộ sinh số ngẫu nhiên bảo mật/);
});
