export interface AssignmentResponse {
  id: string; assetId: string; assetCode: string; assetName: string; userId: string; username: string;
  assignedAt: string; returnedAt: string | null; status: 'Active' | 'Returned' | 'Revoked';
  assignedBy: string; notes: string; closedBy: string | null; closeNotes: string | null;
}
