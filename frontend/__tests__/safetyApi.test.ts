import { describe, it, expect, vi, beforeEach } from "vitest";
import { safetyApi } from "../lib/api/safety";
import { apiClient } from "../lib/api/client";

describe("Frontend Safety API Client", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  describe("safetyApi.createReport", () => {
    it("should send POST to /api/v1/reports with report payload", async () => {
      const mockReport = {
        id: "rep-1",
        reporterUserId: "user-1",
        targetPostId: "post-1",
        category: "SPAM",
        status: "PENDING",
        createdAt: "2026-09-05T00:00:00Z",
      };

      const postSpy = vi.spyOn(apiClient, "post").mockResolvedValueOnce({
        success: true,
        data: mockReport,
        message: "Report submitted.",
      });

      const result = await safetyApi.createReport({
        targetPostId: "post-1",
        category: "SPAM",
        description: "Bot comment",
      });

      expect(postSpy).toHaveBeenCalledWith("/api/v1/reports", {
        targetPostId: "post-1",
        category: "SPAM",
        description: "Bot comment",
      });
      expect(result.data.id).toBe("rep-1");
      expect(result.data.category).toBe("SPAM");
    });
  });

  describe("safetyApi.getAdminReports", () => {
    it("should send GET to /api/v1/admin/reports with query params", async () => {
      const mockResult = {
        items: [],
        page: 2,
        pageSize: 15,
        totalCount: 0,
        totalPages: 0,
      };

      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: mockResult,
      });

      const result = await safetyApi.getAdminReports({
        status: "PENDING",
        category: "HARASSMENT",
        page: 2,
        pageSize: 15,
      });

      expect(getSpy).toHaveBeenCalledWith("/api/v1/admin/reports", {
        params: {
          status: "PENDING",
          category: "HARASSMENT",
          targetType: undefined,
          page: 2,
          pageSize: 15,
        },
      });
      expect(result.data.page).toBe(2);
    });
  });

  describe("safetyApi.executeModerationAction", () => {
    it("should send POST to /api/v1/admin/reports/:id/action with action payload", async () => {
      const mockActionResponse = {
        reportId: "rep-123",
        action: "HIDE_POST",
        status: "RESOLVED",
        message: "Post hidden",
        timestamp: "2026-09-05T00:00:00Z",
      };

      const postSpy = vi.spyOn(apiClient, "post").mockResolvedValueOnce({
        success: true,
        data: mockActionResponse,
      });

      const result = await safetyApi.executeModerationAction(
        "rep-123",
        "HIDE_POST",
        "Violates guidelines"
      );

      expect(postSpy).toHaveBeenCalledWith("/api/v1/admin/reports/rep-123/action", {
        action: "HIDE_POST",
        reason: "Violates guidelines",
      });
      expect(result.data.status).toBe("RESOLVED");
    });
  });
});
