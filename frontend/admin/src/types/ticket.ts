export type TicketCategory = 'Hardware' | 'Software' | 'Network' | 'Account' | 'Storage' | 'Other';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Critical';
export type TicketStatus = 'Open' | 'Assigned' | 'InProgress' | 'Resolved' | 'Closed' | 'Cancelled';
export interface CreateTicketRequest { title: string; description: string; category: TicketCategory; priority: TicketPriority }
export interface TicketResponse extends CreateTicketRequest {
  id: string; ticketNumber: string; status: TicketStatus; createdByUserId: string; createdByName: string;
  assignedToUserId: string | null; assignedToName: string | null; createdAt: string; updatedAt: string | null;
}
