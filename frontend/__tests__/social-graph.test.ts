import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import React from "react";
import { useSocialGraph, useFollowers, useFollowing } from "../hooks/useSocialGraph";
import { socialApi } from "../lib/api/social";
import type { UserProfile } from "../types/api";

vi.mock("../lib/api/social", () => ({
  socialApi: {
    followUser: vi.fn(),
    unfollowUser: vi.fn(),
    getFollowers: vi.fn(),
    getFollowing: vi.fn(),
    blockUser: vi.fn(),
    unblockUser: vi.fn(),
  },
}));

vi.mock("../hooks/useToast", () => ({
  useToast: () => ({
    success: vi.fn(),
    error: vi.fn(),
    info: vi.fn(),
  }),
}));

vi.mock("../hooks/useAuth", () => ({
  useAuth: () => ({
    user: { id: "user-actor", username: "actor" },
    isAuthenticated: true,
  }),
}));

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
  return ({ children }: { children: React.ReactNode }) =>
    React.createElement(QueryClientProvider, { client: queryClient }, children);
};

describe("Frontend Social Graph & Isolation", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe("socialApi Client Abstraction", () => {
    it("should provide all required social graph endpoints", () => {
      expect(typeof socialApi.followUser).toBe("function");
      expect(typeof socialApi.unfollowUser).toBe("function");
      expect(typeof socialApi.getFollowers).toBe("function");
      expect(typeof socialApi.getFollowing).toBe("function");
      expect(typeof socialApi.blockUser).toBe("function");
      expect(typeof socialApi.unblockUser).toBe("function");
    });
  });

  describe("useSocialGraph Hook Mutations", () => {
    it("should execute followUser mutation successfully", async () => {
      const targetId = "target-user-123";
      vi.mocked(socialApi.followUser).mockResolvedValue({
        success: true,
        data: {
          targetUserId: targetId,
          isFollowing: true,
          isBlocked: false,
        },
      });

      const { result } = renderHook(() => useSocialGraph(targetId, "bob"), {
        wrapper: createWrapper(),
      });

      let res;
      await act(async () => {
        res = await result.current.followUser(targetId);
      });

      expect(socialApi.followUser).toHaveBeenCalledWith(targetId);
      expect(res).toEqual({
        targetUserId: targetId,
        isFollowing: true,
        isBlocked: false,
      });
    });

    it("should execute unfollowUser mutation successfully", async () => {
      const targetId = "target-user-123";
      vi.mocked(socialApi.unfollowUser).mockResolvedValue({
        success: true,
        data: {
          targetUserId: targetId,
          isFollowing: false,
          isBlocked: false,
        },
      });

      const { result } = renderHook(() => useSocialGraph(targetId, "bob"), {
        wrapper: createWrapper(),
      });

      let res;
      await act(async () => {
        res = await result.current.unfollowUser(targetId);
      });

      expect(socialApi.unfollowUser).toHaveBeenCalledWith(targetId);
      expect(res).toEqual({
        targetUserId: targetId,
        isFollowing: false,
        isBlocked: false,
      });
    });

    it("should execute blockUser mutation and remove follow relationships", async () => {
      const targetId = "target-user-123";
      vi.mocked(socialApi.blockUser).mockResolvedValue({
        success: true,
        data: {
          targetUserId: targetId,
          isBlocked: true,
          isFollowing: false,
        },
      });

      const { result } = renderHook(() => useSocialGraph(targetId, "spammer"), {
        wrapper: createWrapper(),
      });

      let res;
      await act(async () => {
        res = await result.current.blockUser(targetId);
      });

      expect(socialApi.blockUser).toHaveBeenCalledWith(targetId);
      expect(res).toEqual({
        targetUserId: targetId,
        isBlocked: true,
        isFollowing: false,
      });
    });

    it("should execute unblockUser mutation without restoring follows", async () => {
      const targetId = "target-user-123";
      vi.mocked(socialApi.unblockUser).mockResolvedValue({
        success: true,
        data: {
          targetUserId: targetId,
          isBlocked: false,
          isFollowing: false,
        },
      });

      const { result } = renderHook(() => useSocialGraph(targetId, "spammer"), {
        wrapper: createWrapper(),
      });

      let res;
      await act(async () => {
        res = await result.current.unblockUser(targetId);
      });

      expect(socialApi.unblockUser).toHaveBeenCalledWith(targetId);
      expect(res).toEqual({
        targetUserId: targetId,
        isBlocked: false,
        isFollowing: false,
      });
    });
  });

  describe("useFollowers & useFollowing Query Hooks", () => {
    it("should fetch followers with keyset cursor pagination", async () => {
      const targetId = "target-creator";
      const mockFollower = {
        id: "follower-1",
        username: "fan1",
        displayName: "Fan 1",
        bio: "Bio",
        avatarUrl: null,
        isVerified: false,
        followedAt: new Date().toISOString(),
        followId: "follow-1",
        isFollowing: false,
        isFollowedBy: true,
        isBlocked: false,
      };

      vi.mocked(socialApi.getFollowers).mockResolvedValue({
        success: true,
        data: {
          items: [mockFollower],
          nextCursor: "eyJjIjoxNzI1MDAwLCJpIjoiMDE4ZiJ9",
          hasNextPage: true,
          pageSize: 20,
        },
      });

      const { result } = renderHook(() => useFollowers(targetId, 20), {
        wrapper: createWrapper(),
      });

      // Wait for query to resolve
      await act(async () => {
        await new Promise((resolve) => setTimeout(resolve, 50));
      });

      expect(socialApi.getFollowers).toHaveBeenCalledWith(targetId, null, 20);
      expect(result.current.data?.pages[0].items).toHaveLength(1);
      expect(result.current.data?.pages[0].items[0].username).toBe("fan1");
    });
  });
});
