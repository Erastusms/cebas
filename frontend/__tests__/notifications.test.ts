import { describe, it, expect, vi, beforeEach } from "vitest";
import { notificationsApi } from "../lib/api/notifications";
import { apiClient } from "../lib/api/client";
import type {
  NotificationItem,
  UnreadNotificationCountResponse,
  MarkNotificationReadResponse,
  MarkAllNotificationsReadResponse,
  CursorPagination,
} from "../types/api";

describe("Frontend Notifications API Client", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  describe("notificationsApi.getNotifications", () => {
    it("should send GET to /api/v1/notifications with params and return cursor-paginated list", async () => {
      const mockItems: NotificationItem[] = [
        {
          id: "notif-1",
          actor: {
            id: "user-1",
            username: "johndoe",
            displayName: "John Doe",
            avatarUrl: null,
            isVerified: false,
          },
          type: "POST_LIKED",
          targetId: "post-123",
          targetType: "POST",
          isRead: false,
          readAt: null,
          createdAt: "2026-09-04T12:00:00Z",
        },
      ];

      const mockResponse: CursorPagination<NotificationItem> = {
        items: mockItems,
        nextCursor: "next-cursor-token",
        hasNextPage: true,
        pageSize: 10,
      };

      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: mockResponse,
        message: "Notifications retrieved successfully.",
      });

      const result = await notificationsApi.getNotifications({
        limit: 10,
        unreadOnly: true,
      });

      expect(getSpy).toHaveBeenCalledWith("/api/v1/notifications?limit=10&unread_only=true");
      expect(result.data.items).toHaveLength(1);
      expect(result.data.items[0].type).toBe("POST_LIKED");
      expect(result.data.items[0].actor.username).toBe("johndoe");
      expect(result.data.hasNextPage).toBe(true);
    });
  });

  describe("notificationsApi.getUnreadCount", () => {
    it("should send GET to /api/v1/notifications/unread-count and return count", async () => {
      const mockResponse: UnreadNotificationCountResponse = {
        unreadCount: 5,
      };

      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: mockResponse,
        message: "Unread notification count retrieved.",
      });

      const result = await notificationsApi.getUnreadCount();

      expect(getSpy).toHaveBeenCalledWith("/api/v1/notifications/unread-count");
      expect(result.data.unreadCount).toBe(5);
    });
  });

  describe("notificationsApi.markAsRead", () => {
    it("should send PATCH to /api/v1/notifications/:id/read and return confirmation", async () => {
      const mockResponse: MarkNotificationReadResponse = {
        id: "notif-123",
        isRead: true,
        readAt: "2026-09-04T12:05:00Z",
      };

      const patchSpy = vi.spyOn(apiClient, "patch").mockResolvedValueOnce({
        success: true,
        data: mockResponse,
        message: "Notification marked as read.",
      });

      const result = await notificationsApi.markAsRead("notif-123");

      expect(patchSpy).toHaveBeenCalledWith("/api/v1/notifications/notif-123/read");
      expect(result.data.id).toBe("notif-123");
      expect(result.data.isRead).toBe(true);
    });
  });

  describe("notificationsApi.markAllAsRead", () => {
    it("should send PATCH to /api/v1/notifications/read-all and return marked count", async () => {
      const mockResponse: MarkAllNotificationsReadResponse = {
        markedReadCount: 3,
      };

      const patchSpy = vi.spyOn(apiClient, "patch").mockResolvedValueOnce({
        success: true,
        data: mockResponse,
        message: "All notifications marked as read.",
      });

      const result = await notificationsApi.markAllAsRead();

      expect(patchSpy).toHaveBeenCalledWith("/api/v1/notifications/read-all");
      expect(result.data.markedReadCount).toBe(3);
    });
  });
});
