import { useEffect, useState } from 'react';
import { api } from '../api/auth';
import { isDemoMode } from '../demo/mode';
import type { UserResponse } from '../types/auth';
import type { PagedResult } from '../types/api';

export function useAdminFilterOptions(includeAssets = false) {
  const [users, setUsers] = useState<UserResponse[]>([]);
  const [assets, setAssets] = useState<{ id: string; name: string }[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (isDemoMode) return;
    const controller = new AbortController();
    setLoading(true); setError(''); setUsers([]); setAssets([]);
    const loadUsers = async () => {
      const result: UserResponse[] = [];
      for (let page = 1; !controller.signal.aborted; page++) {
        const data = await api<PagedResult<UserResponse>>(`/users?page=${page}&pageSize=100`, { signal: controller.signal });
        result.push(...data.items);
        if (result.length >= data.totalCount || data.items.length === 0) break;
      }
      return result;
    };
    Promise.all([loadUsers(), includeAssets ? api<{ id: string; name: string }[]>('/assets', { signal: controller.signal }) : Promise.resolve([])])
      .then(([u, a]) => { if (!controller.signal.aborted) { setUsers(u); setAssets(a); } })
      .catch(cause => { if (!controller.signal.aborted) setError(`Không tải được danh sách bộ lọc: ${(cause as Error).message}`); })
      .finally(() => { if (!controller.signal.aborted) setLoading(false); });
    return () => controller.abort();
  }, [includeAssets, attempt]);
  return { users, assets, error, loading, retry: () => setAttempt(value => value + 1) };
}
