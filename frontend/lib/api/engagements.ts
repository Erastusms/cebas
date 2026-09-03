import { apiClient } from "./client";
import type { ApiResponse, CursorPagination } from "./types";
import type { LikeResponse, BookmarkResponse, BookmarkedPost } from "../../types/api";

export const engagementsApi = {
  /**
   * Adds a Like to a post for the authenticated user.
   */
  createLike: async (postId: string): Promise<ApiResponse<LikeResponse>> => {
    return apiClient.post<ApiResponse<LikeResponse>>(`/api/v1/posts/${encodeURIComponent(postId)}/likes`);
  },

  /**
   * Removes a Like from a post for the authenticated user.
   */
  removeLike: async (postId: string): Promise<ApiResponse<LikeResponse>> => {
    return apiClient.delete<ApiResponse<LikeResponse>>(`/api/v1/posts/${encodeURIComponent(postId)}/likes`);
  },

  /**
   * Adds a Bookmark to a post for the authenticated user.
   */
  createBookmark: async (postId: string): Promise<ApiResponse<BookmarkResponse>> => {
    return apiClient.post<ApiResponse<BookmarkResponse>>(`/api/v1/posts/${encodeURIComponent(postId)}/bookmarks`);
  },

  /**
   * Removes a Bookmark from a post for the authenticated user.
   */
  removeBookmark: async (postId: string): Promise<ApiResponse<BookmarkResponse>> => {
    return apiClient.delete<ApiResponse<BookmarkResponse>>(`/api/v1/posts/${encodeURIComponent(postId)}/bookmarks`);
  },

  /**
   * Retrieves bookmarks belonging to the authenticated user using keyset cursor pagination.
   */
  getBookmarks: async (
    cursor?: string | null,
    limit = 20
  ): Promise<ApiResponse<CursorPagination<BookmarkedPost>>> => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());

    const qs = params.toString();
    const endpoint = `/api/v1/bookmarks${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<CursorPagination<BookmarkedPost>>>(endpoint);
  },
};
