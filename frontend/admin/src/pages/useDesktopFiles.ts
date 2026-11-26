import { useEffect, useRef, useState } from 'react';
import { api } from '../api/auth';
import { ApiError } from '../api/client';
import { createMachineApi, encodeBytes, fileAllowed, type FileOperation, type MachineProfile, type ProfileFile } from '../api/machines';
import { createUuid } from '../utils/uuid';

const { operate } = createMachineApi(api);
type DialogAction = 'create' | 'rename' | 'move' | 'copy';

export function useDesktopFiles(initialProfile: MachineProfile, onSessionExpired: (message: string) => void) {
  const [profile, setProfile] = useState(initialProfile);
  const [folder, setFolder] = useState('Desktop');
  const [selected, setSelected] = useState<ProfileFile | null>(null);
  const [text, setText] = useState<string | null>(null);
  const [pdf, setPdf] = useState('');
  const [deleted, setDeleted] = useState<string | null>(null);
  const [action, setAction] = useState<DialogAction | null>(null);
  const [name, setName] = useState('');
  const [target, setTarget] = useState('Documents');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const inFlight = useRef(false);
  const retryIds = useRef(new Map<string, string>());
  const lifetime = useRef<AbortController | null>(null);

  useEffect(() => {
    const controller = new AbortController();
    lifetime.current = controller;
    return () => controller.abort();
  }, []);
  useEffect(() => () => { if (pdf) URL.revokeObjectURL(pdf); }, [pdf]);

  function select(file: ProfileFile | null) {
    setSelected(file);
    setText(null);
    setPdf('');
  }

  function changeFolder(value: string) {
    setFolder(value);
    select(null);
  }

  function openContent(base64: string, filename: string) {
    const bytes = Uint8Array.from(atob(base64), char => char.charCodeAt(0));
    if (/\.(txt|csv)$/i.test(filename)) {
      setText(new TextDecoder().decode(bytes));
      return;
    }
    const isPdf = /\.pdf$/i.test(filename);
    const blob = new Blob([bytes], { type: isPdf ? 'application/pdf' : 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    if (isPdf) {
      setPdf(url);
    } else {
      const link = document.createElement('a');
      link.href = url;
      link.download = filename;
      link.click();
      window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
  }

  // Một lần thử lại sau lỗi mạng dùng lại mã của cùng thao tác trong phiên.
  async function run(input: FileOperation | (() => Promise<FileOperation>)) {
    const signal = lifetime.current?.signal;
    if (!signal || signal.aborted || inFlight.current) return;
    inFlight.current = true;
    setBusy(true);
    setError('');
    let key: string | undefined;
    try {
      const operation = typeof input === 'function' ? await input() : input;
      if (signal.aborted) return;
      key = JSON.stringify(operation);
      const requestId = retryIds.current.get(key) ?? createUuid();
      retryIds.current.set(key, requestId);
      const snapshot = await operate(initialProfile.deviceId, initialProfile.sessionId, { ...operation, requestId }, signal);
      if (signal.aborted) return;
      retryIds.current.delete(key);
      setProfile(snapshot);
      setAction(null);
      if (operation.kind === 'delete') setDeleted(operation.fileId ?? null);
      if (operation.kind === 'restore') setDeleted(null);
      if (operation.kind === 'read' && snapshot.contentBase64 != null)
        openContent(snapshot.contentBase64, selected?.name ?? 'document');
      else if (operation.kind !== 'list') select(null);
    } catch (cause) {
      if (signal.aborted) return;
      const uncertain = cause instanceof TypeError || (cause instanceof ApiError && [408, 429, 502, 503, 504].includes(cause.status));
      if (key && !uncertain) retryIds.current.delete(key);
      const message = (cause as Error).message;
      if (cause instanceof ApiError && [401, 403].includes(cause.status)) onSessionExpired(message);
      else setError(message);
    } finally {
      inFlight.current = false;
      if (!signal.aborted) setBusy(false);
    }
  }

  function importFile(file: File) {
    if (!fileAllowed(file.name) || file.size > 8 * 1024 * 1024) {
      setError('Chỉ nhận tài liệu Office, PDF, TXT, CSV, desktop.ini tối đa 8 MiB.');
      return;
    }
    return run(async () => ({
      kind: 'create', folder, name: file.name, contentBase64: encodeBytes(new Uint8Array(await file.arrayBuffer())),
    }));
  }

  function openDialog(kind: DialogAction) {
    setName(kind === 'create' ? 'document.txt' : selected?.name ?? '');
    setAction(kind);
  }

  function submitDialog() {
    if (action === 'create') {
      if (!/\.(txt|csv)$/i.test(name)) { setError('Tạo tài liệu mới chỉ hỗ trợ TXT/CSV.'); return; }
      return run({ kind: action, folder, name, contentBase64: '' });
    }
    if (!selected || !action) return;
    const fileId = selected.fileId;
    if (action === 'rename') return run({ kind: action, fileId, newName: name });
    if (action === 'move') return run({ kind: action, fileId, targetFolder: target });
    return run({ kind: action, fileId, newName: name, targetFolder: target });
  }

  function saveText() {
    if (selected && text !== null)
      return run({ kind: 'write', fileId: selected.fileId, contentBase64: encodeBytes(new TextEncoder().encode(text)) });
  }

  return {
    profile, folder, selected, text, setText, pdf, deleted, error, busy, run, select, changeFolder, importFile,
    action, openDialog, closeDialog: () => setAction(null), name, setName, target, setTarget, submitDialog, saveText,
  };
}
