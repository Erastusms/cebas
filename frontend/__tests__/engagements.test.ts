import { describe, it, expect, vi, beforeEach } from "vitest";
import { engagementsApi } from "../lib/api/engagements";
import { apiClient } from "../lib/api/client";
import type { LikeResponse, BookmarkResponse, CursorPagination, BookmarkedPost } from "../types/api";

describe("Frontend Engagements API Client & Operations", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  describe("engagementsApi.createLike", () => {
    it("should send POST to /api/v1/posts/:id/likes and return authoritative LikeResponse", async () => {
      const mockResponse: LikeResponse = {
        postId: "post-123",
        liked: true,
        likeCount: 42,
      };

      const postSpy = vi.spyOn(apiClient, "post").mockResolvedValueOnce({
        success: true,
        data: mockResponse,
        message: "Post liked successfully.",
      });

      const result = await engagementsApi.createLike("post-123");

      expect(postSpy).toHaveBeenCalledWith("/api/v1/posts/post-123/likes");
      expect(result.data.liked).toBe(true);
      expect(result.data.likeCount).toBe(42);
      expect(result.data.postId).toBe("post-123");
    });
  });

  describe("engagementsApi.removeLike", () => {
    it("should send DELETE to /api/v1/posts/:id/likes and return updated LikeResponse", async () => {
      const mockResponse: LikeResponse = {
        postId: "post-123",
        liked: false,
        likeCount: 41,
      };

      const deleteSpy = vi.spyOn(apiClient, "delete").mockResolvedValueOnce({
        success: true,
        data: mockResponse,
        message: "Post unliked successfully.",
      });

      const result = await engagementsApi.removeLike("post-123");

      expect(deleteSpy).toHaveBeenCalledWith("/api/v1/posts/post-123/likes");
      expect(result.data.liked).toBe(false);
      expect(result.data.likeCount).toBe(41);
    });
  });

  describe("engagementsApi.createBookmark", () => {
    it("should send POST to /api/v1/posts/:id/bookmarks and return BookmarkResponse", async () => {
      const mockResponse: BookmarkResponse = {
        postId: "post-456",
        bookmarked: true,
        bookmarkCount: 10,
      };

      const postSpy = vi.spyOn(apiClient, "post").mockResolvedValueOnce({
        success: true,
        data: mockResponse,
        message: "Post bookmarked successfully.",
      });

      const result = await engagementsApi.createBookmark("post-456");

      expect(postSpy).toHaveBeenCalledWith("/api/v1/posts/post-456/bookmarks");
      expect(result.data.bookmarked).toBe(true);
      expect(result.data.bookmarkCount).toBe(10);
    });
  });

  describe("engagementsApi.removeBookmark", () => {
    it("should send DELETE to /api/v1/posts/:id/bookmarks and return updated BookmarkResponse", async () => {
      const mockResponse: BookmarkResponse = {
        postId: "post-456",
        bookmarked: false,
        bookmarkCount: 9,
      };

      const deleteSpy = vi.spyOn(apiClient, "delete").mockResolvedValueOnce({
        success: true,
        data: mockResponse,
        message: "Bookmark removed successfully.",
      });

      const result = await engagementsApi.removeBookmark("post-456");

      expect(deleteSpy).toHaveBeenCalledWith("/api/v1/posts/post-456/bookmarks");
      expect(result.data.bookmarked).toBe(false);
      expect(result.data.bookmarkCount).toBe(9);
    });
  });

  describe("engagementsApi.getBookmarks", () => {
    it("should send GET to /api/v1/bookmarks with cursor and limit params", async () => {
      const mockBookmarks: CursorPagination<BookmarkedPost> = {
        items: [
          {
            bookmarkId: "bm-1",
            bookmarkedAt: new Date().toISOString(),
            id: "post-100",
            content: "Saved post content",
            author: {
              id: "user-1",
              username: "author1",
              displayName: "Author One",
              avatarUrl: null,
              isVerified: true,
            },
            media: [],
            replyCount: 2,
            mediaCount: 0,
            likeCount: 15,
            bookmarkCount: 4,
            liked: true,
            bookmarked: true,
            isDeleted: false,
            createdAt: new Date().toISOString(),
          },
        ],
        nextCursor: "cursor-next",
        hasNextPage: true,
        pageSize: 20,
      };

      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: mockBookmarks,
      });

      const result = await engagementsApi.getBookmarks("cursor-prev", 20);

      expect(getSpy).toHaveBeenCalledWith("/api/v1/bookmarks?cursor=cursor-prev&limit=20");
      expect(result.data.items).toHaveLength(1);
      expect(result.data.items[0].bookmarked).toBe(true);
      expect(result.data.items[0].bookmarkId).toBe("bm-1");
      expect(result.data.hasNextPage).toBe(true);
    });
  });
});
