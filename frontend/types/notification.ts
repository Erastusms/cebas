export type NotificationType =
  | "POST_LIKED"
  | "POST_REPLIED"
  | "REPLY_LIKED"
  | "USER_FOLLOWED"
  | "USER_MENTIONED";

export interface NotificationActor {
  id: string;
  username: string;
  displayName: string;
  avatarUrl: string | null;
  isVerified: boolean;
}

export interface NotificationItem {
  id: string;
  actor: NotificationActor;
  type: NotificationType | string;
  targetId: string | null;
  targetType: string | null;
  isRead: boolean;
  readAt: string | null;
  createdAt: string;
}

export interface UnreadNotificationCountResponse {
  unreadCount: number;
}

export interface MarkNotificationReadResponse {
  id: string;
  isRead: boolean;
  readAt: string | null;
}

export interface MarkAllNotificationsReadResponse {
  markedReadCount: number;
}

export interface NotificationCreatedEvent {
  notificationId: string;
  recipientId: string;
  actorId: string;
  type: string;
  targetId: string | null;
  targetType: string | null;
  createdAt: string;
}

export interface PostLikedEvent {
  postId: string;
  actorUserId: string;
  likeCount: number;
  authorUserId: string;
  likedAt: string;
}

export interface PostUnlikedEvent {
  postId: string;
  actorUserId: string;
  likeCount: number;
  authorUserId: string;
  unlikedAt: string;
}

export interface ReplyCreatedEvent {
  replyId: string;
  postId: string;
  authorUserId: string;
  parentReplyId: string | null;
  replyCount: number;
  createdAt: string;
}

export interface NewPostAvailableEvent {
  postId: string;
  authorId: string;
  authorUsername: string;
  authorDisplayName: string;
  authorAvatarUrl: string | null;
  createdAt: string;
}
