import { apiClient } from "./client";
import type { ApiResponse, CursorPagination } from "./types";
import type {
  Post,
  CreatePostRequest,
  CreateReplyRequest,
  ReplyItem,
  HierarchicalRepliesResult,
} from "../../types/api";

export const postsApi = {
  /**
   * Retrieves home timeline feed of posts with cursor pagination.
   */
  getFeed: async (
    cursor?: string | null,
    limit: number = 20
  ): Promise<ApiResponse<CursorPagination<Post>>> => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());

    const qs = params.toString();
    const endpoint = `/api/v1/posts${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<CursorPagination<Post>>>(endpoint);
  },

  /**
   * Retrieves posts published by a specific user for profile tabs with cursor pagination.
   */
  getUserPosts: async (
    username: string,
    filter: string = "posts",
    cursor?: string | null,
    limit: number = 20
  ): Promise<ApiResponse<CursorPagination<Post>>> => {
    const params = new URLSearchParams();
    if (filter) params.set("filter", filter);
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());

    const qs = params.toString();
    const endpoint = `/api/v1/users/${encodeURIComponent(username)}/posts${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<CursorPagination<Post>>>(endpoint);
  },

  /**
   * Retrieves replies authored by a specific user for the profile replies tab with cursor pagination.
   */
  getUserReplies: async (
    username: string,
    cursor?: string | null,
    limit: number = 20
  ): Promise<ApiResponse<CursorPagination<import("../../types/api").UserReply>>> => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());

    const qs = params.toString();
    const endpoint = `/api/v1/users/${encodeURIComponent(username)}/replies${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<CursorPagination<import("../../types/api").UserReply>>>(endpoint);
  },

  /**
   * Creates a new short-form post with optional text and up to 4 attached media assets.
   */
  createPost: async (data: CreatePostRequest): Promise<ApiResponse<Post>> => {
    return apiClient.post<ApiResponse<Post>>("/api/v1/posts", data);
  },

  /**
   * Retrieves full post details by stable identifier.
   */
  getPost: async (id: string): Promise<ApiResponse<Post>> => {
    return apiClient.get<ApiResponse<Post>>(`/api/v1/posts/${encodeURIComponent(id)}`);
  },

  /**
   * Soft-deletes a post by ID. (Only authorized author can delete).
   */
  deletePost: async (id: string): Promise<ApiResponse<object>> => {
    return apiClient.delete<ApiResponse<object>>(`/api/v1/posts/${encodeURIComponent(id)}`);
  },

  /**
   * Creates a direct or nested reply to a post.
   */
  createReply: async (
    postId: string,
    data: CreateReplyRequest
  ): Promise<ApiResponse<ReplyItem>> => {
    return apiClient.post<ApiResponse<ReplyItem>>(
      `/api/v1/posts/${encodeURIComponent(postId)}/replies`,
      data
    );
  },

  /**
   * Retrieves a deterministic hierarchical tree of replies for a post with cursor pagination.
   */
  getReplies: async (
    postId: string,
    cursor?: string | null,
    limit: number = 50
  ): Promise<ApiResponse<HierarchicalRepliesResult>> => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());

    const qs = params.toString();
    const endpoint = `/api/v1/posts/${encodeURIComponent(postId)}/replies${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<HierarchicalRepliesResult>>(endpoint);
  },

  /**
   * Soft-deletes a reply by ID. Preserves child replies in conversation hierarchy.
   */
  deleteReply: async (replyId: string): Promise<ApiResponse<object>> => {
    return apiClient.delete<ApiResponse<object>>(`/api/v1/replies/${encodeURIComponent(replyId)}`);
  },
};
