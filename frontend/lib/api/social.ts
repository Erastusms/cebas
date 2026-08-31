import { apiClient } from "./client";
import type { ApiResponse, CursorPagination } from "./types";
import type { FollowResponse, BlockResponse, SocialUser } from "../../types/api";

export const socialApi = {
  followUser: async (targetId: string): Promise<ApiResponse<FollowResponse>> => {
    return apiClient.post<ApiResponse<FollowResponse>>(`/api/v1/users/${encodeURIComponent(targetId)}/follow`);
  },

  unfollowUser: async (targetId: string): Promise<ApiResponse<FollowResponse>> => {
    return apiClient.delete<ApiResponse<FollowResponse>>(`/api/v1/users/${encodeURIComponent(targetId)}/follow`);
  },

  getFollowers: async (
    targetId: string,
    cursor?: string | null,
    limit = 20
  ): Promise<ApiResponse<CursorPagination<SocialUser>>> => {
    return apiClient.get<ApiResponse<CursorPagination<SocialUser>>>(
      `/api/v1/users/${encodeURIComponent(targetId)}/followers`,
      {
        params: {
          cursor: cursor ?? undefined,
          limit,
        },
      }
    );
  },

  getFollowing: async (
    targetId: string,
    cursor?: string | null,
    limit = 20
  ): Promise<ApiResponse<CursorPagination<SocialUser>>> => {
    return apiClient.get<ApiResponse<CursorPagination<SocialUser>>>(
      `/api/v1/users/${encodeURIComponent(targetId)}/following`,
      {
        params: {
          cursor: cursor ?? undefined,
          limit,
        },
      }
    );
  },

  blockUser: async (targetId: string): Promise<ApiResponse<BlockResponse>> => {
    return apiClient.post<ApiResponse<BlockResponse>>(`/api/v1/users/${encodeURIComponent(targetId)}/block`);
  },

  unblockUser: async (targetId: string): Promise<ApiResponse<BlockResponse>> => {
    return apiClient.delete<ApiResponse<BlockResponse>>(`/api/v1/users/${encodeURIComponent(targetId)}/block`);
  },
};
