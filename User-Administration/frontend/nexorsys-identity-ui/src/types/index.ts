declare module "*.css";
declare module "*.scss";
declare module "*.svg";
declare module "*.png";
declare module "*.jpg";
declare module "*.jpeg";
declare module "*.gif";
declare module "*.json";
declare module "react-chartjs-2";

interface User {
  id: string;
  adGuid: string;
  samAccountName: string;
  displayName: string;
  firstName?: string;
  lastName?: string;
  email?: string;
  department?: string;
  title?: string;
  managerAdGuid?: string;
  employeeId?: string;
  badgeUid?: string;
  isActive: boolean;
  isLocalProfile: boolean;
  createdAt: Date;
  updatedAt: Date;
}

interface UserPin {
  id: string;
  userId: string;
  badgeUid: string;
  pinHash: string;
  isActive: boolean;
  createdAt: Date;
  updatedAt: Date;
  createdBy?: string;
}

interface Application {
  id: string;
  name: string;
  description?: string;
  clientId: string;
  clientSecret?: string;
  redirectUris: string[];
  isActive: boolean;
  createdAt: Date;
}

interface UserPermission {
  id: string;
  userId: string;
  applicationId: string;
  permissionLevel: "read" | "write" | "admin";
  grantedAt: Date;
  grantedBy?: string;
  expiresAt?: Date;
}

interface Workflow {
  id: string;
  type: string;
  userId: string;
  status: "pending" | "approved" | "rejected";
  assignedTo?: string;
  assignedBy?: string;
  comments?: string;
  formData: Record<string, any>;
  createdAt: Date;
  updatedAt: Date;
}

interface AuditLog {
  id: string;
  userId?: string;
  action: string;
  resourceType?: string;
  resourceId?: string;
  oldValues?: Record<string, any>;
  newValues?: Record<string, any>;
  ipAddress?: string;
  userAgent?: string;
  createdAt: Date;
}

interface KioskSession {
  id: string;
  userId: string;
  sessionToken: string;
  badgeUid: string;
  nfcUid: string;
  expiresAt: Date;
  createdAt: Date;
}
