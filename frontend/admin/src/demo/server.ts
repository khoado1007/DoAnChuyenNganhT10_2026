import type { UserResponse } from '../types/auth.ts';
import type { AssetResponse } from '../types/asset.ts';
import type { MachineProfile, ProfileFile } from '../api/machines.ts';

type DemoFile = ProfileFile & { content: string };
type Profile = { id: string; userId: string; deviceId: string; maxFiles: number; files: DemoFile[]; trash: DemoFile[] };
type Session = { id: string; userId: string; deviceId: string; profileId: string };
type Assignment = { id: string; assetId: string; userId: string; username: string; status: string; assignedAt: string; assignedBy: string; notes: string; returnedAt: string | null; closedBy: string | null; closeNotes: string | null };
const uid = () => crypto.randomUUID();
const now = () => new Date().toISOString();
const response = (data: unknown, status = 200, message = '') => Response.json({ success: status < 400, data, message, errors: null }, { status });
const fail = (message: string, status = 400) => response(null, status, message);

/** Adapter chỉ phục vụ demo UI, hoàn toàn không gọi backend hay Agent. Reset khi tải lại trang. */
export function createDemoFetch(): typeof fetch {
  const users: UserResponse[] = [
    { id: 'demo-admin', username: 'admin', fullName: 'Nguyễn Minh Quân', email: 'admin@company.demo', department: 'Công nghệ thông tin', role: 'Admin', status: 'Active', createdAt: now() },
    { id: 'demo-khoa', username: 'khoa', fullName: 'Trần Đăng Khoa', email: 'khoa@company.demo', department: 'Phát triển sản phẩm', role: 'Employee', status: 'Active', createdAt: now() },
    { id: 'demo-an', username: 'an', fullName: 'Nguyễn Hoài An', email: 'an@company.demo', department: 'Kế toán', role: 'Employee', status: 'Active', createdAt: now() },
    { id: 'demo-tech', username: 'linh', fullName: 'Phạm Thùy Linh', email: 'linh@company.demo', department: 'Hỗ trợ kỹ thuật', role: 'Technician', status: 'Active', createdAt: now() },
  ];
  const passwords = new Map(users.map(u => [u.id, 'Demo@12345']));
  const assets: AssetResponse[] = [
    { id: 'pc-001', assetCode: 'PC-001', name: 'Máy trạm phòng Sản phẩm', type: 'Desktop', status: 'Assigned', serialNumber: 'DEMO-DL-2026-01', manufacturer: 'Dell', model: 'OptiPlex 7010', location: 'Tầng 2 · Phòng Sản phẩm', notes: 'Thiết bị dùng chung theo quyền đăng nhập.', createdAt: now(), updatedAt: null },
    { id: 'pc-002', assetCode: 'PC-002', name: 'Máy trạm mô phỏng', type: 'Desktop', status: 'Available', serialNumber: 'DEMO-SIM-002', manufacturer: 'Simulation', model: 'Virtual Workstation', location: 'Phòng thực hành', notes: 'Agent mô phỏng cho bài trình diễn.', createdAt: now(), updatedAt: null },
    { id: 'pc-003', assetCode: 'PC-003', name: 'Laptop phòng Kế toán', type: 'Laptop', status: 'Available', serialNumber: 'DEMO-LN-2026-03', manufacturer: 'Lenovo', model: 'ThinkPad E14', location: 'Tầng 1 · Phòng Kế toán', notes: '', createdAt: now(), updatedAt: null },
  ];
  const profiles: Profile[] = [];
  const sessions: Session[] = [];
  const grants = new Set(['pc-001:demo-khoa', 'pc-001:demo-an', 'pc-002:demo-khoa', 'pc-002:demo-an', 'pc-003:demo-an']);
  const assignments: Assignment[] = [{ id: uid(), assetId: 'pc-001', userId: 'demo-khoa', username: 'khoa', status: 'Active', assignedAt: now(), assignedBy: 'admin', notes: 'Cấp phát cho nhóm Sản phẩm', returnedAt: null, closedBy: null, closeNotes: null }];
  const agents = assets.slice(0, 2).map((asset, index) => ({ id: `agent-${index}`, assetId: asset.id, assetCode: asset.assetCode, assetStatus: asset.status, instanceId: `DEMO-${index + 1}`, policy: 'active', mode: index === 0 ? 'real' : 'simulated', hostname: asset.assetCode, os: 'Windows 11 · dữ liệu demo', cpu: 'Intel Core i5', ramBytes: 16 * 1024 ** 3, agentVersion: 'Demo UI', revokedAt: null as string | null, revokedReason: null as string | null }));
  const replay = new Map<string, string>();
  function snapshot(session: Session, profile: Profile, contentBase64?: string): MachineProfile {
    const used = profile.files.length;
    return { sessionId: session.id, deviceId: session.deviceId, profileId: profile.id,
      domainId: `COMPANY\\${users.find(u => u.id === session.userId)!.username}`, deviceName: assets.find(a => a.id === session.deviceId)!.name,
      files: [...profile.files, ...profile.trash.map(file => ({ ...file, folder: 'RecycleBin' }))].map(({ content: _content, ...file }) => file),
      quota: { maxFiles: profile.maxFiles, usedFiles: used, reservedFiles: 0, remainingFiles: Math.max(0, profile.maxFiles - used), overQuota: used > profile.maxFiles }, contentBase64 };
  }
  function page<T extends object>(rows: T[], url: URL) {
    const search = (url.searchParams.get('search') ?? '').toLocaleLowerCase('vi');
    const selected = rows.filter(row => JSON.stringify(row).toLocaleLowerCase('vi').includes(search));
    const pageSize = Math.min(100, Math.max(1, Number(url.searchParams.get('pageSize')) || 10));
    const page = Math.max(1, Number(url.searchParams.get('page')) || 1);
    return { items: selected.slice((page - 1) * pageSize, page * pageSize), totalCount: selected.length, page, pageSize, totalPages: Math.ceil(selected.length / pageSize), hasNextPage: page * pageSize < selected.length, hasPreviousPage: page > 1 };
  }
  return async (input, init = {}) => {
    if (init.signal?.aborted) throw new DOMException('Request aborted', 'AbortError');
    const url = new URL(String(input), 'http://demo.local');
    const path = url.pathname.replace(/^\/api/, '');
    const method = init.method ?? 'GET';
    const headers = new Headers(init.headers);
    const token = headers.get('Authorization')?.replace('Bearer preview:', '');
    const actor = users.find(u => u.id === token && u.status === 'Active');
    const body = init.body ? JSON.parse(String(init.body)) : {};
    if (path === '/auth/login') {
      const username = String(body.username ?? '').trim().replace(/^COMPANY\\/i, '').toLowerCase();
      const user = users.find(u => u.username.toLowerCase() === username && u.status === 'Active');
      if (!user || body.password !== passwords.get(user.id)) return fail('Sai tài khoản demo hoặc mật khẩu. Dùng Demo@12345.', 401);
      return response({ accessToken: `preview:${user.id}`, user });
    }
    if (path === '/machines') return response(assets.filter(a => !['Disabled', 'Retired', 'UnderMaintenance'].includes(a.status)).map(a => ({ id: a.id, assetCode: a.assetCode, name: a.name, mode: agents.find(x => x.assetId === a.id)?.mode ?? 'simulated' })));
    if (path === '/not-implemented') return fail('Chức năng chưa có trong demo.', 404);
    if (!actor) return fail('Vui lòng đăng nhập tài khoản demo.', 401);
    if (path === '/auth/me') return response(actor);
    if (path === '/me/assets') return response(assignments.filter(a => a.userId === actor.id && a.status === 'Active').map(a => ({ ...a, assetName: assets.find(x => x.id === a.assetId)?.name, assetCode: assets.find(x => x.id === a.assetId)?.assetCode })));

    const machineMatch = path.match(/^\/machines\/([^/]+)\/(sessions|profile|logout|files\/operations)$/);
    if (machineMatch) {
      const [, deviceId, action] = machineMatch;
      const device = assets.find(a => a.id === deviceId);
      if (!device || ['Disabled', 'Retired', 'UnderMaintenance'].includes(device.status) || actor.role !== 'Employee' || !grants.has(`${deviceId}:${actor.id}`)) return fail('Bạn chưa được cấp quyền sử dụng máy này.', 403);
      let profile = profiles.find(p => p.userId === actor.id && p.deviceId === deviceId);
      if (action === 'sessions') {
        const other = sessions.find(s => s.deviceId === deviceId && s.userId !== actor.id);
        if (other) return fail('Máy đang có phiên của người khác. Hãy đăng xuất phiên đó trước.', 409);
        if (!profile) { profile = { id: uid(), userId: actor.id, deviceId, maxFiles: 10, files: [], trash: [] }; profiles.push(profile); }
        let session = sessions.find(s => s.deviceId === deviceId && s.userId === actor.id);
        if (!session) { session = { id: uid(), userId: actor.id, deviceId, profileId: profile.id }; sessions.push(session); }
        return response(snapshot(session, profile));
      }
      const session = sessions.find(s => s.id === headers.get('X-Device-Session') && s.deviceId === deviceId && s.userId === actor.id);
      if (!session || !profile) return fail('Phiên trên máy không còn hiệu lực.', 403);
      if (action === 'logout') { sessions.splice(sessions.indexOf(session), 1); return response(true); }
      if (action === 'profile') return response(snapshot(session, profile));
      const requestKey = `${profile.id}:${body.requestId}`;
      if (replay.has(requestKey)) {
        if (replay.get(requestKey) !== JSON.stringify(body)) return fail('Mã thao tác đã dùng cho yêu cầu khác.', 409);
        return response(snapshot(session, profile));
      }
      const file = profile.files.find(f => f.fileId === body.fileId);
      const folder = ['move', 'copy', 'restore'].includes(body.kind)
        ? body.targetFolder ?? file?.folder ?? 'Desktop'
        : body.kind === 'create' ? body.folder ?? 'Desktop' : file?.folder ?? body.folder ?? 'Desktop';
      const name = String(body.newName ?? body.name ?? file?.name ?? '').trim();
      const validName = (n: string) => n.length > 0 && n.length <= 180 && !/[\\/:*?"<>|\u0000-\u001f]/.test(n) && n !== '..' && !/[. ]$/.test(n) && /\.(docx?|xlsx?|pptx?|pdf|txt|csv|ini)$/i.test(n);
      const duplicate = (n: string, f: string, id?: string) => profile!.files.some(x => x.fileId !== id && x.folder === f && x.name.toLowerCase() === n.toLowerCase());
      const content = String(body.contentBase64 ?? '');
      if (!['Desktop', 'Documents'].includes(folder) && !(body.kind === 'list' && folder === 'RecycleBin')) return fail('Thư mục không hợp lệ.');
      if (['create', 'write'].includes(body.kind)) {
        try { if (atob(content).length > 8 * 1024 * 1024) return fail('Giới hạn kỹ thuật 8 MiB mỗi file.', 413); }
        catch { return fail('Nội dung file không hợp lệ.'); }
      }
      if (['create', 'copy', 'restore'].includes(body.kind) && profile.files.length >= profile.maxFiles) return fail(`Đã đạt quota ${profile.maxFiles}/${profile.maxFiles} file.`, 409);
      if (['read', 'write', 'rename', 'move', 'copy', 'delete'].includes(body.kind) && !file) return fail('Không tìm thấy file trong profile của bạn.', 404);
      if (['create', 'rename', 'copy'].includes(body.kind) && (!validName(name) || duplicate(name, folder, body.kind === 'rename' ? file?.fileId : undefined))) return fail('Tên file không hợp lệ hoặc đã tồn tại.', 409);
      let readContent: string | undefined;
      switch (body.kind) {
        case 'ensure':
        case 'list': break;
        case 'create': profile.files.push({ fileId: uid(), name, folder, extension: name.slice(name.lastIndexOf('.')), sizeBytes: atob(content).length, content }); break;
        case 'read': readContent = file!.content; break;
        case 'write':
          if (!/\.(txt|csv)$/i.test(file!.name)) return fail('Chỉ chỉnh sửa TXT/CSV trong demo.');
          file!.content = content; file!.sizeBytes = atob(content).length; break;
        case 'rename': file!.name = name; file!.extension = name.slice(name.lastIndexOf('.')); break;
        case 'move':
          if (duplicate(file!.name, folder, file!.fileId)) return fail('Tên file đã tồn tại ở thư mục đích.', 409);
          file!.folder = folder; break;
        case 'copy': profile.files.push({ ...file!, fileId: uid(), name, folder }); break;
        case 'delete': profile.files.splice(profile.files.indexOf(file!), 1); profile.trash.push(file!); break;
        case 'restore': {
          const removed = profile.trash.find(f => f.fileId === body.fileId);
          if (!removed) return fail('Không tìm thấy file trong thùng rác.', 404);
          if (duplicate(removed.name, folder)) return fail('Tên file đã tồn tại ở thư mục đích.', 409);
          profile.trash.splice(profile.trash.indexOf(removed), 1); profile.files.push({ ...removed, folder }); break;
        }
        default: return fail('Thao tác chưa được hỗ trợ trong demo.', 400);
      }
      if (body.requestId && !['list', 'read'].includes(body.kind)) replay.set(requestKey, JSON.stringify(body));
      return response(snapshot(session, profile, readContent));
    }
    if (actor.role !== 'Admin') return fail('Chức năng dành cho quản trị viên.', 403);
    if (path === '/dashboard') return response({ users: users.length, assets: assets.length, assignedAssets: assets.filter(a => a.status === 'Assigned').length, agents: agents.length, profiles: profiles.length });
    if (path === '/users' && method === 'GET') return response(page(users, url));
    if (path === '/users' && method === 'POST') {
      const username = String(body.username ?? '').trim().replace(/^COMPANY\\/i, '');
      if (!username || users.some(u => u.username.toLowerCase() === username.toLowerCase())) return fail('Tên đăng nhập đã tồn tại hoặc không hợp lệ.');
      const user: UserResponse = { id: uid(), username, fullName: body.fullName, email: body.email, role: body.role === 'Technician' ? 'Technician' : 'Employee', department: body.department ?? '', status: 'Active', createdAt: now() };
      users.push(user); passwords.set(user.id, String(body.password)); return response(user);
    }
    if (path.startsWith('/users/') && method === 'PUT') {
      const user = users.find(u => u.id === path.split('/')[2]);
      if (!user || user.role === 'Admin') return fail('Không chỉnh sửa tài khoản này.', 403);
      Object.assign(user, { email: body.email, fullName: body.fullName, department: body.department, role: body.role, status: body.status }); return response(user);
    }
    if (path === '/assets/search') return response(page(assets.filter(a => !url.searchParams.get('status') || a.status === url.searchParams.get('status')), url));
    if (path === '/assets' && method === 'GET') return response(assets);
    if (path === '/assets' && method === 'POST') {
      if (!body.assetCode?.trim() || assets.some(a => a.assetCode.toLowerCase() === body.assetCode.toLowerCase())) return fail('Mã tài sản không hợp lệ hoặc đã tồn tại.');
      const asset: AssetResponse = { id: uid(), assetCode: body.assetCode, name: body.name, type: body.type ?? 'Desktop', status: 'Available', serialNumber: body.serialNumber ?? '', manufacturer: body.manufacturer ?? '', model: body.model ?? '', location: body.location ?? '', notes: body.notes ?? '', createdAt: now(), updatedAt: null };
      assets.push(asset); return response(asset);
    }
    const assetMatch = path.match(/^\/assets\/([^/]+)(?:\/(access|assignments|assign|return|revoke)(?:\/([^/]+))?)?$/);
    if (assetMatch) {
      const [, id, action, userId] = assetMatch; const asset = assets.find(a => a.id === id);
      if (!asset) return fail('Không tìm thấy tài sản.', 404);
      if (!action) { if (method === 'PUT') Object.assign(asset, body, { updatedAt: now() }); return response(asset); }
      if (action === 'access') {
        if (method === 'GET') return response(users.filter(u => grants.has(`${id}:${u.id}`)).map(u => ({ userId: u.id, username: u.username, fullName: u.fullName })));
        if (method === 'DELETE') { grants.delete(`${id}:${userId}`); for (let i = sessions.length - 1; i >= 0; i--) if (sessions[i].deviceId === id && sessions[i].userId === userId) sessions.splice(i, 1); }
        else if (method === 'PUT' && users.some(u => u.id === userId && u.role === 'Employee' && u.status === 'Active')) grants.add(`${id}:${userId}`);
        else return fail('Nhân viên không hợp lệ.');
        return response(true);
      }
      if (action === 'assignments') return response(assignments.filter(a => a.assetId === id));
      if (action === 'assign') {
        const user = users.find(u => u.id === body.userId && u.role === 'Employee' && u.status === 'Active');
        if (!user || asset.status !== 'Available') return fail('Không thể cấp phát tài sản.');
        const assignment: Assignment = { id: uid(), assetId: id, userId: user.id, username: user.username, status: 'Active', assignedAt: now(), assignedBy: actor.username, notes: body.notes ?? '', returnedAt: null, closedBy: null, closeNotes: null };
        assignments.push(assignment); asset.status = 'Assigned'; return response(assignment);
      }
      const assignment = assignments.find(a => a.id === body.assignmentId && a.assetId === id && a.status === 'Active');
      if (!assignment) return fail('Lượt cấp phát không còn hoạt động.');
      Object.assign(assignment, { status: action === 'revoke' ? 'Revoked' : 'Returned', returnedAt: now(), closedBy: actor.username, closeNotes: body.notes ?? '' }); asset.status = 'Available';
      if (action === 'revoke') { for (const key of grants) if (key.startsWith(`${id}:`)) grants.delete(key); for (let i = sessions.length - 1; i >= 0; i--) if (sessions[i].deviceId === id) sessions.splice(i, 1); }
      return response(assignment);
    }
    if (path === '/profiles') return response(profiles.map(p => ({ profileId: p.id, userId: p.userId, username: users.find(u => u.id === p.userId)?.username, deviceId: p.deviceId, deviceName: assets.find(a => a.id === p.deviceId)?.name, maxFiles: p.maxFiles, usedFiles: p.files.length })));
    if (/^\/profiles\/[^/]+\/quota$/.test(path) && method === 'PUT') {
      const profile = profiles.find(p => p.id === path.split('/')[2]);
      if (!profile) return fail('Không tìm thấy profile.', 404);
      if (!Number.isInteger(body.maxFiles) || body.maxFiles < 0 || body.maxFiles > 10000) return fail('Quota phải là số nguyên từ 0 đến 10000.');
      profile.maxFiles = body.maxFiles; return response(true);
    }
    if (path === '/agents') return response(agents);
    if (/^\/agents\/[^/]+\/disable$/.test(path)) { const agent = agents.find(a => a.id === path.split('/')[2]); if (agent) agent.policy = 'disabled'; return response(true); }
    if (['/agents/enrollment-tokens', '/agents/recovery-tokens'].includes(path)) return response({ token: `DEMO-NOT-A-REAL-CREDENTIAL-${uid()}`, expiresAt: new Date(Date.now() + 15 * 60000).toISOString() });
    return fail('Chức năng chưa có trong bản xem trước giao diện.', 404);
  };
}
