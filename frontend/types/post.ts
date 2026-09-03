export type PostAuthor = {
  id: string;
  username: string;
  displayName: string;
  avatarUrl?: string | null;
  isVerified: boolean;
};

export type PostMedia = {
  id: string;
  url: string;
  originalFileName?: string | null;
  mimeType?: string | null;
  position: number;
};

export type Post = {
  id: string;
  content: string;
  author: PostAuthor;
  media: PostMedia[];
  replyCount: number;
  mediaCount: number;
  likeCount: number;
  bookmarkCount: number;
  liked: boolean;
  bookmarked: boolean;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string | null;
};

export type CreatePostRequest = {
  content?: string;
  mediaIds?: string[];
};

export type CreateReplyRequest = {
  content: string;
  parentReplyId?: string | null;
};

export type ReplyAuthor = {
  id: string;
  username: string;
  displayName: string;
  avatarUrl?: string | null;
  isVerified: boolean;
};

export type ReplyItem = {
  id: string;
  postId: string;
  parentReplyId?: string | null;
  content: string;
  author?: ReplyAuthor | null;
  depth: number;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string | null;
};

export type HierarchicalRepliesResult = {
  items: ReplyItem[];
  nextCursor?: string | null;
  hasNextPage: boolean;
  pageSize: number;
};

export type UserReply = {
  id: string;
  postId: string;
  parentReplyId?: string | null;
  content: string;
  author: ReplyAuthor;
  replyingToUsername?: string | null;
  parentPostContent?: string | null;
  isDeleted: boolean;
  createdAt: string;
  updatedAt?: string | null;
};
