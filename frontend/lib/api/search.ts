import { apiClient } from "./client";
import type { ApiResponse, CursorPagination } from "./types";
import type { PostAuthor, PostMedia } from "../../types/api";

export interface SearchPostItem {
  id: string;
  content: string;
  highlightedContent?: string | null;
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
  score?: number | null;
}

export interface SearchUserItem {
  id: string;
  username: string;
  displayName: string;
  bio?: string | null;
  avatarUrl?: string | null;
  isVerified: boolean;
  highlightedUsername?: string | null;
  highlightedDisplayName?: string | null;
  highlightedBio?: string | null;
  isFollowing: boolean;
  score?: number | null;
}

export interface SearchSummaryResult {
  posts: SearchPostItem[];
  users: SearchUserItem[];
}

export interface SearchPostsParams {
  query?: string;
  cursor?: string | null;
  limit?: number;
}

export interface SearchUsersParams {
  query?: string;
  limit?: number;
}

export interface SearchSummaryParams {
  query?: string;
  postLimit?: number;
  userLimit?: number;
}

export const searchApi = {
  async searchPosts(params: SearchPostsParams): Promise<ApiResponse<CursorPagination<SearchPostItem>>> {
    return apiClient.get<ApiResponse<CursorPagination<SearchPostItem>>>("/api/v1/search/posts", {
      params: {
        q: params.query,
        cursor: params.cursor ?? undefined,
        limit: params.limit ?? 20,
      },
    });
  },

  async searchUsers(params: SearchUsersParams): Promise<ApiResponse<SearchUserItem[]>> {
    return apiClient.get<ApiResponse<SearchUserItem[]>>("/api/v1/search/users", {
      params: {
        q: params.query,
        limit: params.limit ?? 20,
      },
    });
  },

  async searchSummary(params: SearchSummaryParams): Promise<ApiResponse<SearchSummaryResult>> {
    return apiClient.get<ApiResponse<SearchSummaryResult>>("/api/v1/search", {
      params: {
        q: params.query,
        postLimit: params.postLimit ?? 5,
        userLimit: params.userLimit ?? 5,
      },
    });
  },
};
