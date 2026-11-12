type SessionReference = { deviceId: string; sessionId: string };

/** Chỉ giữ mã tham chiếu trong tab; API luôn xác minh quyền trước khi mở lại profile. */
export function createDeviceSessionStore(storage: Pick<Storage, 'getItem' | 'setItem' | 'removeItem'>, key: string) {
  return {
    read(deviceId: string): SessionReference | null {
      try {
        const value = JSON.parse(storage.getItem(key) || 'null');
        if (value?.deviceId === deviceId && typeof value.sessionId === 'string' && value.sessionId.length > 0)
          return { deviceId, sessionId: value.sessionId };
      } catch { storage.removeItem(key); }
      return null;
    },
    save(value: SessionReference) {
      storage.setItem(key, JSON.stringify({ deviceId: value.deviceId, sessionId: value.sessionId }));
    },
    clear() { storage.removeItem(key); },
  };
}
