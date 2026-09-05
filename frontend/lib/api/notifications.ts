import { apiClient } from "./client";
import type { ApiResponse, CursorPagination } from "./types";
import type {
  NotificationItem,
  UnreadNotificationCountResponse,
  MarkNotificationReadResponse,
  MarkAllNotificationsReadResponse,
} from "../../types/api";

export interface GetNotificationsParams {
  cursor?: string | null;
  limit?: number;
  unreadOnly?: boolean;
}

export const notificationsApi = {
  /**
   * Retrieves notifications for the authenticated user with cursor pagination.
   */
  getNotifications: async (
    params: GetNotificationsParams = {}
  ): Promise<ApiResponse<CursorPagination<NotificationItem>>> => {
    const { cursor, limit = 20, unreadOnly = false } = params;
    const query = new URLSearchParams();
    if (cursor) query.set("cursor", cursor);
    if (limit) query.set("limit", limit.toString());
    if (unreadOnly) query.set("unread_only", "true");

    const qs = query.toString();
    const endpoint = `/api/v1/notifications${qs ? `?${qs}` : ""}`;
    return apiClient.get<ApiResponse<CursorPagination<NotificationItem>>>(endpoint);
  },

  /**
   * Retrieves the current count of unread notifications for the authenticated user.
   */
  getUnreadCount: async (): Promise<ApiResponse<UnreadNotificationCountResponse>> => {
    return apiClient.get<ApiResponse<UnreadNotificationCountResponse>>(
      "/api/v1/notifications/unread-count"
    );
  },

  /**
   * Marks a specific notification as read.
   */
  markAsRead: async (
    notificationId: string
  ): Promise<ApiResponse<MarkNotificationReadResponse>> => {
    return apiClient.patch<ApiResponse<MarkNotificationReadResponse>>(
      `/api/v1/notifications/${encodeURIComponent(notificationId)}/read`
    );
  },

  /**
   * Marks all unread notifications as read.
   */
  markAllAsRead: async (): Promise<ApiResponse<MarkAllNotificationsReadResponse>> => {
    return apiClient.patch<ApiResponse<MarkAllNotificationsReadResponse>>(
      "/api/v1/notifications/read-all"
    );
  },
};
