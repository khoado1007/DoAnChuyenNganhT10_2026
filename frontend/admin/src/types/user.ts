import type { UserRole } from './auth';
export type { UserResponse, UserRole, UserStatus } from './auth';
export interface CreateUserRequest {
  username: string; email: string; fullName: string; password: string; department: string; role: UserRole;
}
