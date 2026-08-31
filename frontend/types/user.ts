export type UserProfileStats = {
  postCount: number;
  followerCount: number;
  followingCount: number;
};

import type { UserProfileRelationship } from "./social";

export type UserProfile = {
  id: string;
  username: string;
  displayName: string;
  bio?: string | null;
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  isVerified: boolean;
  createdAt: string;
  stats: UserProfileStats;
  relationship?: UserProfileRelationship | null;
};

export type UpdateProfileRequest = {
  displayName: string;
  bio?: string | null;
  bannerUrl?: string | null;
};

export type SessionItem = {
  id: string;
  userAgent?: string | null;
  ipAddress?: string | null;
  createdAt: string;
  expiresAt: string;
  isCurrent: boolean;
};
