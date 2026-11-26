import type { MachineSpecification, Software } from '../types/machine-information.ts';
import type { UserResponse } from '../types/auth.ts';
import type { AssetResponse } from '../types/asset.ts';
import type { MachineProfile, ProfileFile } from '../api/machines.ts';
import { createUuid } from '../utils/uuid.ts';

type DemoFile = ProfileFile & { content: string };
type Profile = { id: string; userId: string; deviceId: string; files: DemoFile[]; trash: DemoFile[] };
type Session = { id: string; userId: string; deviceId: string; profileId: string };
type Assignment = { id: string; assetId: string; userId: string; username: string; status: string; assignedAt: string; assignedBy: string; notes: string; returnedAt: string | null; closedBy: string | null; closeNotes: string | null };
type DemoTicket = {
  id: string; ticketNumber: string; title: string; description: string; category: string; priority: string;
  status: string; createdByUserId: string; createdByName: string; assignedToUserId: string | null;
  assignedToName: string | null; createdAt: string; updatedAt: string | null; resolvedAt: string | null;
  resolution: string | null; comments: { id: string; authorUsername: string; content: string; isInternal: boolean; createdAt: string }[];
  history: { fromStatus: string; toStatus: string; changedByUsername: string; note: string | null; changedAt: string }[];
};
const uid = () => createUuid();
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
    { id: 'pc-001', assetCode: 'PC-001', name: 'Máy trạm phòng Sản phẩm', type: 'Desktop', status: 'Assigned', serialNumber: 'DEMO-DL-2026-01', manufacturer: 'Dell', model: 'OptiPlex 7010', location: 'Tầng 2 · Phòng Sản phẩm', notes: 'Thiết bị dùng chung cho nhân viên đang hoạt động.', createdAt: now(), updatedAt: null },
    { id: 'pc-002', assetCode: 'PC-002', name: 'Máy trạm mô phỏng', type: 'Desktop', status: 'Available', serialNumber: 'DEMO-SIM-002', manufacturer: 'Simulation', model: 'Virtual Workstation', location: 'Phòng thực hành', notes: 'Máy mô phỏng cho bài trình diễn.', createdAt: now(), updatedAt: null },
    { id: 'pc-003', assetCode: 'PC-003', name: 'Laptop phòng Kế toán', type: 'Laptop', status: 'Available', serialNumber: 'DEMO-LN-2026-03', manufacturer: 'Lenovo', model: 'ThinkPad E14', location: 'Tầng 1 · Phòng Kế toán', notes: '', createdAt: now(), updatedAt: null },
  ];
  const specifications = new Map<string, Partial<MachineSpecification>>();
  const information = (asset: AssetResponse) => ({ ...asset, assetId: asset.id, revision: '0', ...specifications.get(asset.id), software: specifications.get(asset.id)?.software ?? [], status: 'simulated' });
  const employeeInfo = (asset: AssetResponse) => {
    const info = information(asset);
    return { ...info, software: info.software.map(item => ({ ...item, filePath: null })) };
  };
  const approvalAttempts = new Map<string, number[]>();
  const profiles: Profile[] = [];
  const limits = new Map<string, number>();
  const accountLimit = (userId: string) => limits.get(userId) ?? 10;
  const accountUsed = (userId: string) => profiles.reduce((sum, p) => sum + (p.userId === userId ? p.files.length : 0), 0);
  const fileHistory = new Map<string, { owner: string; fileName: string; kind: string; completedAt: string }[]>();
  const sessions: Session[] = [];
  const grants = new Set(['pc-001:demo-khoa', 'pc-001:demo-an', 'pc-002:demo-khoa', 'pc-002:demo-an', 'pc-003:demo-an']);
  const assignments: Assignment[] = [{ id: uid(), assetId: 'pc-001', userId: 'demo-khoa', username: 'khoa', status: 'Active', assignedAt: now(), assignedBy: 'admin', notes: 'Cấp phát cho nhóm Sản phẩm', returnedAt: null, closedBy: null, closeNotes: null }];
  const tickets: DemoTicket[] = [
    { id: uid(), ticketNumber: 'TKT-2026-0001', title: 'Máy trạm không kết nối được mạng nội bộ', description: 'Từ sáng nay máy PC-001 không mở được thư mục dùng chung. Đã thử khởi động lại máy nhưng vẫn lỗi.', category: 'Network', priority: 'High', status: 'Open', createdByUserId: 'demo-khoa', createdByName: 'Trần Đăng Khoa', assignedToUserId: null, assignedToName: null, createdAt: new Date(Date.now() - 3 * 3600000).toISOString(), updatedAt: null, resolvedAt: null, resolution: null, comments: [], history: [] },
    { id: uid(), ticketNumber: 'TKT-2026-0002', title: 'Cần cài thêm công cụ đọc PDF', description: 'Máy laptop phòng Kế toán chưa có phần mềm đọc PDF để xem chứng từ.', category: 'Software', priority: 'Medium', status: 'Resolved', createdByUserId: 'demo-an', createdByName: 'Nguyễn Hoài An', assignedToUserId: 'demo-tech', assignedToName: 'Phạm Thùy Linh', createdAt: new Date(Date.now() - 2 * 86400000).toISOString(), updatedAt: new Date(Date.now() - 86400000).toISOString(), resolvedAt: new Date(Date.now() - 86400000).toISOString(), resolution: 'Đã cài trình đọc PDF và kiểm tra mở chứng từ mẫu.', comments: [
      { id: uid(), authorUsername: 'an', content: 'Nhờ bộ phận CNTT hỗ trợ giúp em.', isInternal: false, createdAt: new Date(Date.now() - 2 * 86400000).toISOString() },
      { id: uid(), authorUsername: 'admin', content: 'Đã xử lý, em kiểm tra lại giúp nhé.', isInternal: false, createdAt: new Date(Date.now() - 86400000).toISOString() },
    ], history: [{ fromStatus: 'Open', toStatus: 'InProgress', changedByUsername: 'admin', note: 'Đã tiếp nhận', changedAt: new Date(Date.now() - 2 * 86400000 + 3600000).toISOString() }, { fromStatus: 'InProgress', toStatus: 'Resolved', changedByUsername: 'admin', note: null, changedAt: new Date(Date.now() - 86400000).toISOString() }] },
  ];
  let ticketSequence = tickets.length;
  const agents = assets.slice(0, 2).map((asset, index) => ({ id: `agent-${index}`, assetId: asset.id, assetCode: asset.assetCode, assetStatus: asset.status, instanceId: `DEMO-${index + 1}`, policy: 'active', mode: index === 0 ? 'real' : 'simulated', hostname: asset.assetCode, os: 'Windows 11 · dữ liệu demo', cpu: 'Intel Core i5', ramBytes: 16 * 1024 ** 3, agentVersion: 'Demo UI', revokedAt: null as string | null, revokedReason: null as string | null }));
  const replay = new Map<string, string>();
  function snapshot(session: Session, profile: Profile, contentBase64?: string): MachineProfile {
    const used = accountUsed(profile.userId), limit = accountLimit(profile.userId);
    return { sessionId: session.id, deviceId: session.deviceId, profileId: profile.id,
      domainId: `COMPANY\\${users.find(u => u.id === session.userId)!.username}`, deviceName: assets.find(a => a.id === session.deviceId)!.name,
      files: [...profile.files, ...profile.trash.map(file => ({ ...file, folder: 'RecycleBin' }))].map(({ content: _content, ...file }) => file),
      quota: { maxFiles: limit, usedFiles: used, reservedFiles: 0, remainingFiles: Math.max(0, limit - used), overQuota: used > limit }, contentBase64 };
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
    if (path === '/machines') return response(assets.filter(a => !['Disabled', 'Retired', 'UnderMaintenance'].includes(a.status)).map(a => ({ id: a.id, assetCode: a.assetCode, name: a.name, mode: 'simulated' })));
    if (path === '/not-implemented') return fail('Chức năng chưa có trong demo.', 404);
    if (!actor) return fail('Vui lòng đăng nhập tài khoản demo.', 401);
    if (path === '/auth/me') return response(actor);
    if (path === '/me/assets') return response(assignments.filter(a => a.userId === actor.id && a.status === 'Active').map(a => ({ ...a, assetName: assets.find(x => x.id === a.assetId)?.name, assetCode: assets.find(x => x.id === a.assetId)?.assetCode })));

    const machineMatch = path.match(/^\/machines\/([^/]+)\/(sessions|profile|information|file-details|logout|files\/operations|software\/([^/]+)\/remove)$/);
    if (machineMatch) {
      const [, deviceId, action] = machineMatch;
      const device = assets.find(a => a.id === deviceId);
      if (!device || ['Disabled', 'Retired', 'UnderMaintenance'].includes(device.status) || actor.role !== 'Employee') return fail('Tài khoản hoặc máy không hoạt động, hoặc tài khoản không phải nhân viên.', 403);
      let profile = profiles.find(p => p.userId === actor.id && p.deviceId === deviceId);
      if (action === 'sessions') {
        const other = sessions.find(s => s.deviceId === deviceId && s.userId !== actor.id);
        if (other) return fail('Máy đang có phiên của người khác. Hãy đăng xuất phiên đó trước.', 409);
        if (!profile) { profile = { id: uid(), userId: actor.id, deviceId, files: [], trash: [] }; profiles.push(profile); }
        let session = sessions.find(s => s.deviceId === deviceId && s.userId === actor.id);
        if (!session) { session = { id: uid(), userId: actor.id, deviceId, profileId: profile.id }; sessions.push(session); }
        return response(snapshot(session, profile));
      }
      const session = sessions.find(s => s.id === headers.get('X-Device-Session') && s.deviceId === deviceId && s.userId === actor.id);
      if (!session || !profile) return fail('Phiên trên máy không còn hiệu lực.', 403);
      if (action === 'logout') { sessions.splice(sessions.indexOf(session), 1); return response(true); }
      if (action === 'profile') return response(snapshot(session, profile));
      if (action === 'information') return response(employeeInfo(device));
      if (action === 'file-details') {
        const files = profiles.filter(p => p.deviceId === deviceId).flatMap(p => {
          const owner = users.find(u => u.id === p.userId)?.username ?? '';
          return [...p.files, ...p.trash.map(f => ({ ...f, folder: 'RecycleBin' }))].map(
            ({ name, folder, extension, sizeBytes }) => ({ name, folder, extension, sizeBytes, owner }));
        }).sort((a, b) => a.name.localeCompare(b.name) || a.owner.localeCompare(b.owner));
        const detailsPage = <T extends object>(rows: T[], key: string) => {
          const params = new URL(url.origin);
          params.searchParams.set('page', String(Math.min(Number(url.searchParams.get(key)) || 1, Math.ceil(rows.length / 10))));
          return page(rows, params);
        };
        return response({ files: detailsPage(files, 'filesPage'), history: detailsPage(fileHistory.get(deviceId) ?? [], 'historyPage') });
      }
      if (machineMatch[3]) {
        if (method !== 'POST') return fail('Thao tác không hợp lệ.', 405);
        const attempts = (approvalAttempts.get(actor.id) ?? []).filter(time => Date.now() - time < 60000);
        if (attempts.length >= 5) return fail('Quá nhiều yêu cầu. Vui lòng thử lại sau.', 429);
        approvalAttempts.set(actor.id, [...attempts, Date.now()]);
        const admin = users.find(user => user.role === 'Admin' && user.status === 'Active' && user.username.toLowerCase() === String(body.adminUsername).trim().replace(/^COMPANY\\/i, '').toLowerCase());
        if (!admin || passwords.get(admin.id) !== body.adminPassword) return fail('Không thể xác nhận tài khoản Admin.', 403);
        const spec = information(device);
        if (spec.revision !== body.revision) return fail('Cấu hình đã thay đổi. Vui lòng tải lại.', 409);
        if (!spec.software.some(item => item.id === machineMatch[3])) return fail('Không tìm thấy phần mềm.', 404);
        specifications.set(deviceId, { ...specifications.get(deviceId), software: spec.software.filter(item => item.id !== machineMatch[3]), revision: uid() });
        return response(employeeInfo(device));
      }
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
      if (['create', 'copy', 'restore'].includes(body.kind) && accountUsed(actor.id) >= accountLimit(actor.id)) return fail('Đã đạt giới hạn file của tài khoản.', 409);
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
      if (!['list', 'read', 'ensure'].includes(body.kind)) {
        const fileName = body.kind === 'restore' ? profile.files.find(f => f.fileId === body.fileId)!.name : name;
        const events = fileHistory.get(deviceId) ?? [];
        events.unshift({ owner: actor.username, kind: body.kind, fileName, completedAt: now() });
        fileHistory.set(deviceId, events);
      }
      if (body.requestId && !['list', 'read'].includes(body.kind)) replay.set(requestKey, JSON.stringify(body));
      return response(snapshot(session, profile, readContent));
    }
    if (path.startsWith('/tickets')) {
      const visible = (ticket: DemoTicket) => actor.role === 'Admin' || ticket.createdByUserId === actor.id
        || (actor.role === 'Technician' && ticket.assignedToUserId === actor.id);
      const listRow = (ticket: DemoTicket) => ({
        id: ticket.id, ticketNumber: ticket.ticketNumber, title: ticket.title, category: ticket.category,
        priority: ticket.priority, status: ticket.status, createdByName: ticket.createdByName,
        assignedToName: ticket.assignedToName,
        createdAt: ticket.createdAt, updatedAt: ticket.updatedAt,
      });
      const detailRow = (ticket: DemoTicket) => ({
        ...listRow(ticket), description: ticket.description, createdByUserId: ticket.createdByUserId,
        assignedToUserId: ticket.assignedToUserId, assignedToName: ticket.assignedToName,
        resolvedAt: ticket.resolvedAt, resolution: ticket.resolution,
        comments: ticket.comments.filter(comment => actor.role !== 'Employee' || !comment.isInternal),
        history: ticket.history,
      });
      if (path === '/tickets' && method === 'GET') {
        const search = (url.searchParams.get('search') ?? '').toLocaleLowerCase('vi');
        const status = url.searchParams.get('status');
        const category = url.searchParams.get('category');
        const priority = url.searchParams.get('priority');
        const rows = tickets.filter(visible).filter(ticket =>
          (!search || `${ticket.ticketNumber} ${ticket.title} ${ticket.createdByName}`.toLocaleLowerCase('vi').includes(search))
          && (!status || ticket.status === status) && (!category || ticket.category === category)
          && (!priority || ticket.priority === priority));
        const pageSize = Math.min(100, Math.max(1, Number(url.searchParams.get('pageSize')) || 20));
        const pageNumber = Math.max(1, Number(url.searchParams.get('page')) || 1);
        return response({
          items: rows.slice((pageNumber - 1) * pageSize, pageNumber * pageSize).map(listRow),
          totalCount: rows.length, page: pageNumber, pageSize,
          totalPages: Math.ceil(rows.length / pageSize), hasPreviousPage: pageNumber > 1,
          hasNextPage: pageNumber * pageSize < rows.length,
        });
      }
      if (path === '/tickets' && method === 'POST') {
        if (actor.role !== 'Employee') return fail('Chỉ nhân viên được gửi yêu cầu hỗ trợ.', 403);
        const title = String(body.title ?? '').trim();
        const description = String(body.description ?? '').trim();
        if (title.length < 5 || description.length < 10) return fail('Tiêu đề tối thiểu 5 ký tự và mô tả tối thiểu 10 ký tự.');
        if (!['Hardware', 'Software', 'Network', 'Account', 'Storage', 'Other'].includes(body.category)
          || !['Low', 'Medium', 'High', 'Critical'].includes(body.priority)) return fail('Danh mục hoặc ưu tiên không hợp lệ.');
        const ticket: DemoTicket = {
          id: uid(), ticketNumber: `TKT-${new Date().getFullYear()}-${String(++ticketSequence).padStart(4, '0')}`,
          title, description, category: body.category, priority: body.priority, status: 'Open',
          createdByUserId: actor.id, createdByName: actor.fullName, assignedToUserId: null, assignedToName: null,
          createdAt: now(), updatedAt: null, resolvedAt: null, resolution: null, comments: [], history: [],
        };
        tickets.push(ticket);
        return response(detailRow(ticket));
      }
      const ticketMatch = path.match(/^\/tickets\/([^/]+)(?:\/(status|comments|assign))?$/);
      if (ticketMatch) {
        const [, id, action] = ticketMatch;
        const ticket = tickets.find(item => item.id === id);
        if (!ticket || !visible(ticket)) return fail('Không tìm thấy yêu cầu hỗ trợ.', 404);
        if (!action && method === 'GET') return response(detailRow(ticket));
        if (action === 'comments' && method === 'POST') {
          const content = String(body.content ?? '').trim();
          if (!content) return fail('Nội dung bình luận không được để trống.');
          if (body.isInternal && actor.role === 'Employee') return fail('Nhân viên không được tạo ghi chú nội bộ.', 403);
          const comment = { id: uid(), authorUsername: actor.username, content,
            isInternal: Boolean(body.isInternal) && actor.role !== 'Employee', createdAt: now() };
          ticket.comments.push(comment); ticket.updatedAt = now();
          return response(comment);
        }
        if (action === 'assign' && method === 'PATCH') {
          if (actor.role !== 'Admin') return fail('Chỉ quản trị viên được phân công yêu cầu hỗ trợ.', 403);
          if (body.assignedToUserId) {
            const tech = users.find(u => u.id === body.assignedToUserId && u.role === 'Technician' && u.status === 'Active');
            if (!tech) return fail('Kỹ thuật viên không tồn tại hoặc không ở trạng thái hoạt động.', 400);
            ticket.assignedToUserId = tech.id;
            ticket.assignedToName = tech.fullName;
            ticket.history.push({ fromStatus: ticket.status, toStatus: ticket.status, changedByUsername: actor.username,
              note: `Phân công cho ${tech.fullName} (${tech.username}).${body.note ? ` Ghi chú: ${body.note}` : ''}`, changedAt: now() });
          } else {
            ticket.assignedToUserId = null;
            ticket.assignedToName = null;
            ticket.history.push({ fromStatus: ticket.status, toStatus: ticket.status, changedByUsername: actor.username,
              note: `Hủy phân công.${body.note ? ` Ghi chú: ${body.note}` : ''}`, changedAt: now() });
          }
          ticket.updatedAt = now();
          return response(detailRow(ticket));
        }
        if (action === 'status' && method === 'PATCH') {
          if (actor.role !== 'Admin' && !(actor.role === 'Technician' && ticket.assignedToUserId === actor.id))
            return fail('Chỉ quản trị viên hoặc kỹ thuật viên được giao mới được cập nhật trạng thái.', 403);
          const flow: Record<string, string> = { Open: 'InProgress', InProgress: 'Resolved', Resolved: 'Closed' };
          if (!flow[ticket.status] || flow[ticket.status] !== body.status) return fail('Chuyển trạng thái không hợp lệ.');
          const resolution = String(body.resolution ?? '').trim();
          if (body.status === 'Resolved' && !resolution) return fail('Vui lòng nhập kết quả xử lý.');
          ticket.history.push({ fromStatus: ticket.status, toStatus: body.status, changedByUsername: actor.username,
            note: body.note ? String(body.note).trim() || null : null, changedAt: now() });
          ticket.status = body.status; ticket.updatedAt = now();
          if (body.status === 'Resolved') { ticket.resolution = resolution; ticket.resolvedAt = now(); }
          return response(detailRow(ticket));
        }
      }
      return fail('Thao tác ticket chưa được hỗ trợ trong demo.', 400);
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
    if (path === '/assets/software/bulk' && method === 'POST') {
      const ids = body.assetIds as string[], inputs = body.software as Software[];
      if (!Array.isArray(ids) || ids.length < 1 || ids.length > 50 || new Set(ids).size !== ids.length ||
          !Array.isArray(inputs) || !inputs.length || inputs.length > 200 || inputs.some(s => !s?.name?.trim() || !['App', 'Service'].includes(s.kind ?? '') || s.id))
        return fail('Danh sách máy hoặc phần mềm không hợp lệ.');
      const machines = [];
      const updates = new Map<string, Software[]>();
      for (const id of ids) {
        const asset = assets.find(a => a.id === id);
        if (!asset) return fail('Không tìm thấy tài sản.', 404);
        const software = [...information(asset).software];
        let added = 0;
        for (const input of inputs) {
          const name = input.name.trim();
          if (software.some(s => s.kind === input.kind && s.name.trim().toLowerCase() === name.toLowerCase())) continue;
          software.push({ ...input, id: uid(), name }); added++;
        }
        if (software.length > 200) return fail('Máy vượt giới hạn 200 phần mềm.');
        if (added) updates.set(id, software);
        machines.push({ assetId: id, assetName: asset.name, added, skipped: inputs.length - added });
      }
      for (const [id, software] of updates) specifications.set(id, { ...specifications.get(id), software, revision: uid() });
      return response({ machines, added: machines.reduce((n, m) => n + m.added, 0), skipped: machines.reduce((n, m) => n + m.skipped, 0) });
    }
    if (path === '/assets' && method === 'POST') {
      if (!body.assetCode?.trim() || assets.some(a => a.assetCode.toLowerCase() === body.assetCode.toLowerCase())) return fail('Mã tài sản không hợp lệ hoặc đã tồn tại.');
      const asset: AssetResponse = { id: uid(), assetCode: body.assetCode, name: body.name, type: body.type ?? 'Desktop', status: 'Available', serialNumber: body.serialNumber ?? '', manufacturer: body.manufacturer ?? '', model: body.model ?? '', location: body.location ?? '', notes: body.notes ?? '', createdAt: now(), updatedAt: null };
      assets.push(asset); return response(asset);
    }
    const assetMatch = path.match(/^\/assets\/([^/]+)(?:\/(specification|access|assignments|assign|return|revoke)(?:\/([^/]+))?)?$/);
    if (assetMatch) {
      const [, id, action, userId] = assetMatch; const asset = assets.find(a => a.id === id);
      if (!asset) return fail('Không tìm thấy tài sản.', 404);
      if (!action) { if (method === 'PUT') Object.assign(asset, body, { updatedAt: now() }); return response(asset); }
      if (action === 'specification') {
        if (method === 'PUT') {
          const current = information(asset);
          if ((body.revision ?? '0') !== current.revision) return fail('Cấu hình đã thay đổi. Vui lòng tải lại.', 409);
          const software = (body.software ?? []) as Software[];
          if (software.some(s => s.id && !current.software.some(existing => existing.id === s.id))) return fail('Phần mềm đã thay đổi.', 409);
          specifications.set(id, { ...body, revision: uid(), software: software.map(s => ({ ...s, id: s.id ?? uid() })) });
        }
        return response(information(asset));
      }
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
    if (path === '/profiles') return response(profiles.map(p => ({ profileId: p.id, userId: p.userId, username: users.find(u => u.id === p.userId)?.username, deviceId: p.deviceId, deviceName: assets.find(a => a.id === p.deviceId)?.name, maxFiles: accountLimit(p.userId), usedFiles: p.files.length, storageBackend: 'Server' })));
    if (/^\/profiles\/[^/]+\/quota$/.test(path) && method === 'PUT') {
      const profile = profiles.find(p => p.id === path.split('/')[2]);
      if (!profile) return fail('Không tìm thấy profile.', 404);
      if (!Number.isInteger(body.maxFiles) || body.maxFiles < 0 || body.maxFiles > 10000) return fail('Quota phải là số nguyên từ 0 đến 10000.');
      limits.set(profile.userId, body.maxFiles); return response(true);
    }
    if (path === '/agents') return response(agents);
    if (/^\/agents\/[^/]+\/disable$/.test(path)) { const agent = agents.find(a => a.id === path.split('/')[2]); if (agent) agent.policy = 'disabled'; return response(true); }
    if (['/agents/enrollment-tokens', '/agents/recovery-tokens'].includes(path)) return response({ token: `DEMO-NOT-A-REAL-CREDENTIAL-${uid()}`, expiresAt: new Date(Date.now() + 15 * 60000).toISOString() });
    return fail('Chức năng chưa có trong bản xem trước giao diện.', 404);
  };
}
