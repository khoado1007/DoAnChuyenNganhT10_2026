import { useEffect, useRef, useState } from 'react';
import { api, clearSession, deviceSessions, endMachineSession, getToken, reportMachinePage } from '../api/auth';
import { ApiError } from '../api/client';
import { createMachineApi, type DeviceSession, type MachineProfile } from '../api/machines';
import type { UserResponse } from '../types/auth';

const { operate, profileRequest } = createMachineApi(api);

export function useMachineSession(
  device: string, navigate: (path: string, replace?: boolean) => void,
  onUserChange: (user: UserResponse | null) => void,
) {
  const [profile, setProfile] = useState<MachineProfile | null>(null);
  const [restoring, setRestoring] = useState(true);
  const [restoreError, setRestoreError] = useState('');
  const [attempt, setAttempt] = useState(0);
  const [error, setError] = useState('');
  const [leaving, setLeaving] = useState(false);
  const [passwordChanged, setPasswordChanged] = useState(false);
  const active = useRef<AbortController | null>(null);
  const loginPath = `/machines/${device}/login`;
  const desktopPath = `/machines/${device}/desktop`;

  useEffect(() => {
    const controller = new AbortController();
    const { signal } = controller;
    active.current = controller;
    setProfile(null);
    setRestoring(true);
    setRestoreError('');
    setError('');
    setLeaving(false);
    setPasswordChanged(false);

    async function restore() {
      const saved = getToken() ? deviceSessions.read(device || undefined) : null;
      if (!device && saved) {
        navigate(`/machines/${saved.deviceId}/desktop`, true);
        return;
      }
      if (!saved) {
        if (device && window.location.pathname === desktopPath) navigate(loginPath, true);
        setRestoring(false);
        return;
      }
      try {
        await reportMachinePage(false);
        const snapshot = await profileRequest(device, saved.sessionId, signal);
        if (signal.aborted) return;
        if (snapshot.deviceId !== device || snapshot.sessionId !== saved.sessionId)
          throw new Error('Máy chủ trả về phiên không khớp với máy đã chọn.');
        setProfile(snapshot);
        if (window.location.pathname !== desktopPath) navigate(desktopPath, true);
      } catch (cause) {
        if (signal.aborted) return;
        if (cause instanceof ApiError && [400, 401, 403, 404].includes(cause.status)) {
          clearSession();
          onUserChange(null);
          navigate(loginPath, true);
          setError('Phiên đã hết hạn hoặc quyền sử dụng máy đã thay đổi. Vui lòng đăng nhập lại.');
        } else {
          setRestoreError('Chưa thể kiểm tra phiên trên máy. Kiểm tra kết nối và thử lại.');
        }
      } finally {
        if (!signal.aborted) setRestoring(false);
      }
    }
    void restore();
    return () => controller.abort();
  }, [device, loginPath, desktopPath, attempt, navigate, onUserChange]);

  function returnToLogin(message = '') {
    setProfile(null);
    setLeaving(false);
    setError(message);
    clearSession();
    onUserChange(null);
    navigate(loginPath, true);
  }

  async function enter(user: UserResponse) {
    const signal = active.current?.signal;
    if (!signal || signal.aborted) return;
    setPasswordChanged(false);
    setError('');
    let started: DeviceSession | undefined;
    try {
      started = await api<DeviceSession>(`/machines/${encodeURIComponent(device)}/sessions`, {
        method: 'POST', body: '{}', signal,
      });
      if (signal.aborted) return;
      deviceSessions.save(started);
      const snapshot = await operate(device, started.sessionId, { kind: 'ensure', folder: 'Desktop' }, signal);
      if (signal.aborted) return;
      setProfile(snapshot);
      onUserChange(user);
      navigate(desktopPath, true);
    } catch (cause) {
      if (signal.aborted) return;
      try {
        if (started) await endMachineSession(started);
        if (!signal.aborted) clearSession();
      } catch (cleanup) {
        if (!signal.aborted) setRestoreError(`Chưa thể đóng phiên vừa tạo. Hãy thử khôi phục phiên. ${(cleanup as Error).message}`);
      }
      if (!signal.aborted) {
        onUserChange(null);
        setError((cause as Error).message);
      }
    }
  }

  async function leave() {
    const signal = active.current?.signal;
    if (!profile || !signal || signal.aborted || leaving) return;
    setLeaving(true);
    setError('');
    try {
      await endMachineSession(profile);
      if (!signal.aborted) returnToLogin();
    } catch (cause) {
      if (!signal.aborted) {
        setError((cause as Error).message);
        setLeaving(false);
      }
    }
  }

  function afterPasswordChange() {
    returnToLogin();
    setPasswordChanged(true);
  }

  return {
    profile: profile?.deviceId === device ? profile : null, error, restoring, restoreError, leaving,
    passwordChanged, enter, leave, returnToLogin, afterPasswordChange, retry: () => setAttempt(n => n + 1),
  };
}
