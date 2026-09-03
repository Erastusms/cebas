import type { Post } from "./post";

export type LikeResponse = {
  postId: string;
  liked: boolean;
  likeCount: number;
};

export type BookmarkResponse = {
  postId: string;
  bookmarked: boolean;
  bookmarkCount: number;
};

export type BookmarkedPost = Post & {
  bookmarkId: string;
  bookmarkedAt: string;
  postId?: string;
};
