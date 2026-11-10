export interface FileResponse {
  id: string;
  displayName: string;
  sizeBytes: number;
  uploadedAt: string;
  virtualFolder: string;
}

export interface PersonalQuotaResponse {
  quotaBytes: number;
  usedBytes: number;
  availableBytes: number;
  maxFileBytes: number;
}

export interface FileQuery {
  page?: number;
  pageSize?: number;
}
