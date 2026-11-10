export type UserRole = 'Admin' | 'Technician' | 'Employee';
export type UserStatus = 'Active' | 'Locked' | 'Inactive';

export interface UserResponse {
  id: string;
  username: string;
  email: string;
  fullName: string;
  department: string;
  role: UserRole;
  status: UserStatus;
  createdAt: string;
}

export interface LoginRequest { username: string; password: string }
export interface AuthResponse { accessToken: string; user: UserResponse }
