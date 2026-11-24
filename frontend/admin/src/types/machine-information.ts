export type Software = {
  id?: string | null; kind?: 'App' | 'Service' | null;
  name: string; version: string | null; type: string | null; vulnerabilities: string | null;
  lastUsedAt: string | null; filePath: string | null;
};
export type MachineSpecification = {
  revision?: string;
  operatingSystem: string | null; processor: string | null; memoryGb: number | null;
  diskTotalGb: number | null; diskFreeGb: number | null; diskEncryption: boolean | null;
  collectorVersion: string | null; privateIp: string | null; publicIp: string | null;
  lastRestartedAt: string | null; software: Software[];
};
export type MachineInformation = MachineSpecification & {
  assetId: string; assetCode: string; name: string; status: string;
  serialNumber: string; manufacturer: string; model: string;
  createdAt: string; updatedAt: string | null;
};
export const specificationLabels = {
  operatingSystem: 'Hệ điều hành', processor: 'Bộ xử lý', memoryGb: 'RAM (GB)',
  diskTotalGb: 'Tổng ổ đĩa (GB)', diskFreeGb: 'Ổ đĩa còn trống (GB)', diskEncryption: 'Mã hóa ổ đĩa',
  collectorVersion: 'Phiên bản bộ thu thập', privateIp: 'IP nội bộ', publicIp: 'IP công khai', lastRestartedAt: 'Khởi động lại gần nhất',
};
export const displayValue = (value: unknown) => value === null || value === undefined || value === '' ? 'Chưa khai báo' : typeof value === 'boolean' ? (value ? 'Có' : 'Không') : String(value);
export const displayDate = (value: unknown) => value ? new Date(String(value)).toLocaleString('vi-VN') : 'Chưa khai báo';
export const softwareKinds = [{ value: 'App', label: 'Ứng dụng (App)' }, { value: 'Service', label: 'Dịch vụ (Service)' }];
export const softwareKindLabel = (kind: Software['kind']) => softwareKinds.find(option => option.value === kind)?.label ?? 'Chưa phân loại';
