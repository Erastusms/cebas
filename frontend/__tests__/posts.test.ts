import { describe, it, expect, vi, beforeEach } from "vitest";
import { postsApi } from "../lib/api/posts";
import { apiClient } from "../lib/api/client";
import type {
  Post,
  ReplyItem,
  HierarchicalRepliesResult,
} from "../types/api";

describe("Frontend Posts & Replies API Client & Invariants", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  describe("postsApi.createPost", () => {
    it("should send POST to /api/v1/posts with text and media payload", async () => {
      const mockPost: Post = {
        id: "post-123",
        content: "Hello world!",
        author: {
          id: "user-1",
          username: "johndoe",
          displayName: "John Doe",
          avatarUrl: null,
          isVerified: false,
        },
        media: [],
        replyCount: 0,
        mediaCount: 0,
        isDeleted: false,
        createdAt: new Date().toISOString(),
      };

      const postSpy = vi.spyOn(apiClient, "post").mockResolvedValueOnce({
        success: true,
        data: mockPost,
        message: "Post created successfully.",
      });

      const result = await postsApi.createPost({
        content: "Hello world!",
        mediaIds: ["media-1", "media-2"],
      });

      expect(postSpy).toHaveBeenCalledWith("/api/v1/posts", {
        content: "Hello world!",
        mediaIds: ["media-1", "media-2"],
      });
      expect(result.data.id).toBe("post-123");
      expect(result.data.content).toBe("Hello world!");
    });
  });

  describe("postsApi.getPost", () => {
    it("should retrieve post details by ID", async () => {
      const mockPost: Post = {
        id: "post-999",
        content: "Detail content",
        author: {
          id: "user-1",
          username: "alice",
          displayName: "Alice",
          avatarUrl: "/api/v1/media/avatar-1",
          isVerified: true,
        },
        media: [
          {
            id: "media-1",
            url: "/api/v1/media/media-1",
            position: 0,
          },
        ],
        replyCount: 3,
        mediaCount: 1,
        isDeleted: false,
        createdAt: new Date().toISOString(),
      };

      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: mockPost,
      });

      const result = await postsApi.getPost("post-999");
      expect(getSpy).toHaveBeenCalledWith("/api/v1/posts/post-999");
      expect(result.data.id).toBe("post-999");
      expect(result.data.mediaCount).toBe(1);
      expect(result.data.replyCount).toBe(3);
    });
  });

  describe("postsApi.deletePost", () => {
    it("should send DELETE request to /api/v1/posts/:id", async () => {
      const deleteSpy = vi.spyOn(apiClient, "delete").mockResolvedValueOnce({
        success: true,
        data: {},
        message: "Post deleted successfully.",
      });

      const result = await postsApi.deletePost("post-123");
      expect(deleteSpy).toHaveBeenCalledWith("/api/v1/posts/post-123");
      expect(result.success).toBe(true);
    });
  });

  describe("postsApi.createReply", () => {
    it("should send direct and nested reply requests", async () => {
      const mockReply: ReplyItem = {
        id: "reply-1",
        postId: "post-123",
        parentReplyId: "parent-reply-0",
        content: "Nested comment",
        author: {
          id: "user-2",
          username: "bob",
          displayName: "Bob",
          isVerified: false,
        },
        depth: 1,
        isDeleted: false,
        createdAt: new Date().toISOString(),
      };

      const postSpy = vi.spyOn(apiClient, "post").mockResolvedValueOnce({
        success: true,
        data: mockReply,
        message: "Reply created successfully.",
      });

      const result = await postsApi.createReply("post-123", {
        content: "Nested comment",
        parentReplyId: "parent-reply-0",
      });

      expect(postSpy).toHaveBeenCalledWith("/api/v1/posts/post-123/replies", {
        content: "Nested comment",
        parentReplyId: "parent-reply-0",
      });
      expect(result.data.depth).toBe(1);
      expect(result.data.parentReplyId).toBe("parent-reply-0");
    });
  });

  describe("postsApi.getReplies", () => {
    it("should query hierarchical replies with cursor pagination", async () => {
      const mockResult: HierarchicalRepliesResult = {
        items: [
          {
            id: "reply-1",
            postId: "post-123",
            parentReplyId: null,
            content: "Root reply A",
            depth: 0,
            isDeleted: false,
            createdAt: new Date().toISOString(),
          },
          {
            id: "reply-2",
            postId: "post-123",
            parentReplyId: "reply-1",
            content: "Child reply A.1",
            depth: 1,
            isDeleted: false,
            createdAt: new Date().toISOString(),
          },
        ],
        nextCursor: "next-cursor-token",
        hasNextPage: true,
        pageSize: 50,
      };

      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: mockResult,
      });

      const result = await postsApi.getReplies("post-123", "cur-1", 50);
      expect(getSpy).toHaveBeenCalledWith("/api/v1/posts/post-123/replies?cursor=cur-1&limit=50");
      expect(result.data.items).toHaveLength(2);
      expect(result.data.items[0].depth).toBe(0);
      expect(result.data.items[1].depth).toBe(1);
      expect(result.data.hasNextPage).toBe(true);
    });
  });

  describe("postsApi.getUserReplies", () => {
    it("should send GET request to /api/v1/users/:username/replies with cursor pagination", async () => {
      const mockReplies = {
        items: [
          {
            id: "reply-100",
            postId: "post-50",
            parentReplyId: null,
            content: "My thoughtful reply",
            author: {
              id: "user-1",
              username: "johndoe",
              displayName: "John Doe",
              isVerified: true,
            },
            replyingToUsername: "alice",
            parentPostContent: "Original post",
            isDeleted: false,
            createdAt: new Date().toISOString(),
          },
        ],
        nextCursor: null,
        hasNextPage: false,
        pageSize: 20,
      };

      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: mockReplies,
      });

      const result = await postsApi.getUserReplies("johndoe", null, 20);
      expect(getSpy).toHaveBeenCalledWith("/api/v1/users/johndoe/replies?limit=20");
      expect(result.data.items).toHaveLength(1);
      expect(result.data.items[0].replyingToUsername).toBe("alice");
      expect(result.data.items[0].content).toBe("My thoughtful reply");
    });
  });
});
