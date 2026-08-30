export type User = {
  id: string;
  username: string;
  email: string;
  displayName: string;
  bio?: string | null;
  avatarUrl?: string | null;
  role: string;
  isVerified: boolean;
  createdAt: string;
  updatedAt?: string | null;
  sessionId?: string | null;
};

export type RegisterRequest = {
  username: string;
  email: string;
  password: string;
  displayName?: string;
};

export type LoginRequest = {
  identifier: string;
  password: string;
};
