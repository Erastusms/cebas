import { apiClient } from "./client";
import type { ApiResponse } from "./types";
import type { User, RegisterRequest, LoginRequest } from "../../types/auth";

export const authApi = {
  register: async (data: RegisterRequest): Promise<ApiResponse<User>> => {
    return apiClient.post<ApiResponse<User>>("/api/v1/auth/register", data);
  },

  login: async (data: LoginRequest): Promise<ApiResponse<User>> => {
    return apiClient.post<ApiResponse<User>>("/api/v1/auth/login", data);
  },

  logout: async (): Promise<ApiResponse<object>> => {
    return apiClient.post<ApiResponse<object>>("/api/v1/auth/logout");
  },

  getCurrentUser: async (): Promise<ApiResponse<User>> => {
    return apiClient.get<ApiResponse<User>>("/api/v1/users/me");
  },
};
