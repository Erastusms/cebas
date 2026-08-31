export type FollowResponse = {
  targetUserId: string;
  isFollowing: boolean;
  isBlocked: boolean;
};

export type BlockResponse = {
  targetUserId: string;
  isBlocked: boolean;
  isFollowing: boolean;
};

export type SocialUser = {
  id: string;
  username: string;
  displayName: string;
  bio?: string | null;
  avatarUrl?: string | null;
  isVerified: boolean;
  followedAt: string;
  followId: string;
  isFollowing: boolean;
  isFollowedBy: boolean;
  isBlocked: boolean;
};

export type UserProfileRelationship = {
  isFollowing: boolean;
  isFollowedBy: boolean;
  isBlocked: boolean;
  isBlockedBy: boolean;
};
