import { apiClient } from "./client";
import type { ApiResponse, CursorPagination } from "./types";
import type { Post } from "../../types/api";

export const timelinesApi = {
  /**
   * Retrieves the home feed timeline for the authenticated viewer with keyset cursor pagination.
   * Deterministically ordered by created_at DESC, id DESC.
   */
  getHomeTimeline: async (
    cursor?: string | null,
    limit: number = 20
  ): Promise<ApiResponse<CursorPagination<Post>>> => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());

    const qs = params.toString();
    const endpoint = `/api/v1/timelines/home${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<CursorPagination<Post>>>(endpoint);
  },

  /**
   * Retrieves posts published by a specific user with keyset cursor pagination.
   * Supports target user ID (UUID) or canonical username.
   */
  getUserPosts: async (
    idOrUsername: string,
    filter: string = "posts",
    cursor?: string | null,
    limit: number = 20
  ): Promise<ApiResponse<CursorPagination<Post>>> => {
    const params = new URLSearchParams();
    if (filter) params.set("filter", filter);
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());

    const qs = params.toString();
    const endpoint = `/api/v1/users/${encodeURIComponent(idOrUsername)}/posts${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<CursorPagination<Post>>>(endpoint);
  },

  /**
   * Retrieves posts liked by a specific user with keyset cursor pagination.
   * Supports target user ID (UUID) or canonical username.
   */
  getUserLikes: async (
    idOrUsername: string,
    cursor?: string | null,
    limit: number = 20
  ): Promise<ApiResponse<CursorPagination<Post>>> => {
    const params = new URLSearchParams();
    if (cursor) params.set("cursor", cursor);
    if (limit) params.set("limit", limit.toString());

    const qs = params.toString();
    const endpoint = `/api/v1/users/${encodeURIComponent(idOrUsername)}/likes${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<CursorPagination<Post>>>(endpoint);
  },
};
