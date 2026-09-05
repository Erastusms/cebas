import { apiClient } from "./client";
import type { ApiResponse } from "./types";

export interface CreateReportRequest {
  targetPostId?: string | null;
  targetUserId?: string | null;
  category: "SPAM" | "HARASSMENT" | "HATE_SPEECH" | "INAPPROPRIATE_CONTENT" | string;
  description?: string | null;
}

export interface ReportResponse {
  id: string;
  reporterUserId: string;
  targetPostId?: string | null;
  targetUserId?: string | null;
  category: string;
  status: string;
  reason?: string | null;
  createdAt: string;
  resolvedAt?: string | null;
  resolvedByUserId?: string | null;
}

export interface ReportedPostPreview {
  id: string;
  authorId: string;
  authorUsername: string;
  authorDisplayName: string;
  authorAvatarUrl?: string | null;
  content: string;
  mediaUrls: string[];
  createdAt: string;
  isDeleted: boolean;
  isHidden: boolean;
}

export interface ReportedUserPreview {
  id: string;
  username: string;
  displayName: string;
  avatarUrl?: string | null;
  role: string;
  isSuspended: boolean;
  createdAt: string;
}

export interface ReportDetailItem {
  id: string;
  reporterUserId: string;
  reporterUsername: string;
  reporterDisplayName: string;
  reporterAvatarUrl?: string | null;
  category: string;
  status: string;
  reason?: string | null;
  createdAt: string;
  resolvedAt?: string | null;
  resolvedByUserId?: string | null;
}

export interface ModerationReportItem {
  id: string;
  reporterUserId: string;
  reporterUsername: string;
  reporterDisplayName: string;
  reporterAvatarUrl?: string | null;
  targetType: string;
  targetPostId?: string | null;
  targetUserId?: string | null;
  category: string;
  status: string;
  reason?: string | null;
  createdAt: string;
  resolvedAt?: string | null;
  resolvedByUserId?: string | null;
  targetPost?: ReportedPostPreview | null;
  targetUser?: ReportedUserPreview | null;
  reportCount?: number;
  categories?: string[];
  reports?: ReportDetailItem[];
}

export interface PagedReportsResult {
  items: ModerationReportItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ModerationActionResponse {
  reportId: string;
  action: string;
  status: string;
  message: string;
  timestamp: string;
}

export interface SuspendedUserItem {
  id: string;
  username: string;
  displayName: string;
  avatarUrl?: string | null;
  role: string;
  suspendedAt?: string | null;
  suspensionReason?: string | null;
  totalPosts: number;
  createdAt: string;
}

export interface PagedSuspendedUsersResult {
  items: SuspendedUserItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UnsuspendUserResponse {
  userId: string;
  username: string;
  message: string;
  timestamp: string;
}

export const safetyApi = {
  createReport: async (data: CreateReportRequest): Promise<ApiResponse<ReportResponse>> => {
    return apiClient.post<ApiResponse<ReportResponse>>("/api/v1/reports", data);
  },

  getAdminReports: async (params?: {
    status?: string;
    category?: string;
    targetType?: string;
    page?: number;
    pageSize?: number;
  }): Promise<ApiResponse<PagedReportsResult>> => {
    return apiClient.get<ApiResponse<PagedReportsResult>>("/api/v1/admin/reports", {
      params: {
        status: params?.status || undefined,
        category: params?.category || undefined,
        targetType: params?.targetType || undefined,
        page: params?.page ?? 1,
        pageSize: params?.pageSize ?? 25,
      },
    });
  },

  executeModerationAction: async (
    reportId: string,
    action: "RESOLVE" | "DISMISS" | "HIDE_POST" | "SUSPEND_USER" | string,
    reason?: string
  ): Promise<ApiResponse<ModerationActionResponse>> => {
    return apiClient.post<ApiResponse<ModerationActionResponse>>(
      `/api/v1/admin/reports/${reportId}/action`,
      {
        action,
        reason,
      }
    );
  },

  getSuspendedUsers: async (params?: {
    page?: number;
    pageSize?: number;
    search?: string;
  }): Promise<ApiResponse<PagedSuspendedUsersResult>> => {
    return apiClient.get<ApiResponse<PagedSuspendedUsersResult>>("/api/v1/admin/users/suspended", {
      params: {
        page: params?.page ?? 1,
        pageSize: params?.pageSize ?? 25,
        search: params?.search || undefined,
      },
    });
  },

  unsuspendUser: async (
    userId: string,
    reason?: string
  ): Promise<ApiResponse<UnsuspendUserResponse>> => {
    return apiClient.post<ApiResponse<UnsuspendUserResponse>>(
      `/api/v1/admin/users/${userId}/unsuspend`,
      {
        reason,
      }
    );
  },
};

