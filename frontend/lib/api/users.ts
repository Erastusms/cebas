import { apiClient } from "./client";
import type { ApiResponse } from "./types";
import type { User, UserProfile, UpdateProfileRequest, SessionItem } from "../../types/api";

export const usersApi = {
  getPublicProfile: async (username: string): Promise<ApiResponse<UserProfile>> => {
    return apiClient.get<ApiResponse<UserProfile>>(`/api/v1/users/${encodeURIComponent(username)}`);
  },

  updateProfile: async (data: UpdateProfileRequest): Promise<ApiResponse<User>> => {
    return apiClient.patch<ApiResponse<User>>("/api/v1/users/me", data);
  },

  updateBanner: async (data: { mediaId?: string; bannerUrl?: string }): Promise<ApiResponse<User>> => {
    return apiClient.put<ApiResponse<User>>("/api/v1/users/me/banner", data);
  },

  getSessions: async (): Promise<ApiResponse<SessionItem[]>> => {
    return apiClient.get<ApiResponse<SessionItem[]>>("/api/v1/users/me/sessions");
  },

  revokeSession: async (sessionId: string): Promise<ApiResponse<object>> => {
    return apiClient.delete<ApiResponse<object>>(`/api/v1/users/me/sessions/${encodeURIComponent(sessionId)}`);
  },
};
