import { describe, it, expect, vi, beforeEach } from "vitest";
import { timelinesApi } from "../lib/api/timelines";
import { apiClient } from "../lib/api/client";
import type { Post } from "../types/api";

describe("Timelines API & Keyset Cursor Feeds", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("getHomeTimeline should query /api/v1/timelines/home with cursor and limit", async () => {
    const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
      success: true,
      data: {
        items: [],
        nextCursor: "cursor-token-123",
        hasNextPage: true,
        pageSize: 20,
      },
    });

    const result = await timelinesApi.getHomeTimeline("cursor-token-123", 25);

    expect(getSpy).toHaveBeenCalledWith(
      "/api/v1/timelines/home?cursor=cursor-token-123&limit=25"
    );
    expect(result.data.nextCursor).toBe("cursor-token-123");
  });

  it("getUserPosts should query /api/v1/users/{id}/posts with filter and cursor", async () => {
    const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
      success: true,
      data: {
        items: [],
        nextCursor: null,
        hasNextPage: false,
        pageSize: 20,
      },
    });

    await timelinesApi.getUserPosts("user-uuid-1", "media", "cursor-abc", 15);

    expect(getSpy).toHaveBeenCalledWith(
      "/api/v1/users/user-uuid-1/posts?filter=media&cursor=cursor-abc&limit=15"
    );
  });

  it("getUserLikes should query /api/v1/users/{id}/likes with keyset cursor", async () => {
    const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
      success: true,
      data: {
        items: [],
        nextCursor: "like-cursor",
        hasNextPage: true,
        pageSize: 20,
      },
    });

    await timelinesApi.getUserLikes("john_doe", "like-cursor", 20);

    expect(getSpy).toHaveBeenCalledWith(
      "/api/v1/users/john_doe/likes?cursor=like-cursor&limit=20"
    );
  });

  it("should prevent duplicate post IDs when merging cursor pages", () => {
    const mockPostA: Post = {
      id: "post-1",
      content: "First",
      author: { id: "u1", username: "user1", displayName: "User 1", isVerified: false },
      media: [],
      replyCount: 0,
      mediaCount: 0,
      likeCount: 5,
      bookmarkCount: 1,
      liked: false,
      bookmarked: false,
      isDeleted: false,
      createdAt: "2026-09-01T10:00:00Z",
    };

    const mockPostB: Post = {
      id: "post-2",
      content: "Second",
      author: { id: "u2", username: "user2", displayName: "User 2", isVerified: false },
      media: [],
      replyCount: 1,
      mediaCount: 0,
      likeCount: 2,
      bookmarkCount: 0,
      liked: true,
      bookmarked: false,
      isDeleted: false,
      createdAt: "2026-09-01T09:00:00Z",
    };

    const mockPostC: Post = {
      id: "post-3",
      content: "Third",
      author: { id: "u1", username: "user1", displayName: "User 1", isVerified: false },
      media: [],
      replyCount: 0,
      mediaCount: 0,
      likeCount: 0,
      bookmarkCount: 0,
      liked: false,
      bookmarked: false,
      isDeleted: false,
      createdAt: "2026-09-01T08:00:00Z",
    };

    // Simulate Page 1 and Page 2 overlapping post-2
    const pages = [
      { items: [mockPostA, mockPostB], nextCursor: "cur-1", hasNextPage: true, pageSize: 2 },
      { items: [mockPostB, mockPostC], nextCursor: null, hasNextPage: false, pageSize: 2 },
    ];

    // Deduplication logic identical to InfiniteFeed
    const seen = new Set<string>();
    const deduplicated: Post[] = [];
    for (const page of pages) {
      for (const item of page.items) {
        if (!seen.has(item.id)) {
          seen.add(item.id);
          deduplicated.push(item);
        }
      }
    }

    expect(deduplicated).toHaveLength(3);
    expect(deduplicated.map((p) => p.id)).toEqual(["post-1", "post-2", "post-3"]);
  });

  it("should determine next page param correctly based on hasNextPage and nextCursor", () => {
    const getNextPageParam = (lastPage: { hasNextPage: boolean; nextCursor?: string | null }) =>
      lastPage.hasNextPage ? (lastPage.nextCursor ?? undefined) : undefined;

    expect(getNextPageParam({ hasNextPage: true, nextCursor: "tok-1" })).toBe("tok-1");
    expect(getNextPageParam({ hasNextPage: false, nextCursor: null })).toBeUndefined();
    expect(getNextPageParam({ hasNextPage: false, nextCursor: "stale" })).toBeUndefined();
  });
});
