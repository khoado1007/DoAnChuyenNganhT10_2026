export type AssetType = 'Desktop' | 'Laptop' | 'Server' | 'Printer' | 'NetworkDevice' | 'Other';
export type AssetStatus = 'Available' | 'Assigned' | 'UnderMaintenance' | 'Retired' | 'Disabled';
export interface CreateAssetRequest {
  assetCode: string; name: string; type: AssetType;
  serialNumber?: string; manufacturer?: string; model?: string; location?: string; notes?: string;
}
export interface AssetResponse {
  id: string; assetCode: string; name: string; type: AssetType; status: AssetStatus;
  serialNumber: string; manufacturer: string; model: string; location: string; notes: string;
  createdAt: string; updatedAt: string | null;
}
