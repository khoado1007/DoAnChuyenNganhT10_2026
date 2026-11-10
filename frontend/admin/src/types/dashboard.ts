import type { AssetStatus } from './asset';
import type { TicketResponse } from './ticket';
export interface StorageQuotaResponse { quotaBytes: number; usedBytes: number; availableBytes: number }
export interface DashboardResponse {
  totalAssets: number; totalUsers: number; openTickets: number; resolvedThisMonth: number;
  assetsByStatus: { status: AssetStatus; count: number }[]; recentTickets: TicketResponse[];
}
