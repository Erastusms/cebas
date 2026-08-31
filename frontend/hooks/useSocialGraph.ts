"use client";

import { useMutation, useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { socialApi } from "../lib/api/social";
import type { UserProfile, SocialUser } from "../types/api";
import { useToast } from "./useToast";

export function useSocialGraph(targetUserId?: string, targetUsername?: string) {
  const queryClient = useQueryClient();
  const { success, error: toastError } = useToast();

  const profileQueryKey = ["profile", targetUsername?.toLowerCase()];

  // 1. Follow User Mutation with Optimistic UI
  const followMutation = useMutation({
    mutationFn: async (userId: string) => {
      const res = await socialApi.followUser(userId);
      return res.data;
    },
    onMutate: async (userId: string) => {
      // Cancel any outgoing refetches so they don't overwrite our optimistic update
      if (targetUsername) {
        await queryClient.cancelQueries({ queryKey: profileQueryKey });
      }

      // Snapshot previous profile value
      const previousProfile = targetUsername
        ? queryClient.getQueryData<UserProfile | null>(profileQueryKey)
        : null;

      // Optimistically update profile state
      if (previousProfile) {
        queryClient.setQueryData<UserProfile>(profileQueryKey, {
          ...previousProfile,
          stats: {
            ...previousProfile.stats,
            followerCount: previousProfile.stats.followerCount + 1,
          },
          relationship: {
            isFollowing: true,
            isFollowedBy: previousProfile.relationship?.isFollowedBy ?? false,
            isBlocked: false,
            isBlockedBy: false,
          },
        });
      }

      return { previousProfile };
    },
    onError: (err, _userId, context) => {
      if (context?.previousProfile && targetUsername) {
        queryClient.setQueryData(profileQueryKey, context.previousProfile);
      }
      toastError("Failed to follow user. Please try again.", "Error");
    },
    onSettled: () => {
      if (targetUsername) {
        queryClient.invalidateQueries({ queryKey: profileQueryKey });
      }
      queryClient.invalidateQueries({ queryKey: ["following"] });
      queryClient.invalidateQueries({ queryKey: ["followers"] });
    },
  });

  // 2. Unfollow User Mutation with Optimistic UI
  const unfollowMutation = useMutation({
    mutationFn: async (userId: string) => {
      const res = await socialApi.unfollowUser(userId);
      return res.data;
    },
    onMutate: async (userId: string) => {
      if (targetUsername) {
        await queryClient.cancelQueries({ queryKey: profileQueryKey });
      }

      const previousProfile = targetUsername
        ? queryClient.getQueryData<UserProfile | null>(profileQueryKey)
        : null;

      if (previousProfile) {
        queryClient.setQueryData<UserProfile>(profileQueryKey, {
          ...previousProfile,
          stats: {
            ...previousProfile.stats,
            followerCount: Math.max(0, previousProfile.stats.followerCount - 1),
          },
          relationship: {
            isFollowing: false,
            isFollowedBy: previousProfile.relationship?.isFollowedBy ?? false,
            isBlocked: false,
            isBlockedBy: false,
          },
        });
      }

      return { previousProfile };
    },
    onError: (err, _userId, context) => {
      if (context?.previousProfile && targetUsername) {
        queryClient.setQueryData(profileQueryKey, context.previousProfile);
      }
      toastError("Failed to unfollow user. Please try again.", "Error");
    },
    onSettled: () => {
      if (targetUsername) {
        queryClient.invalidateQueries({ queryKey: profileQueryKey });
      }
      queryClient.invalidateQueries({ queryKey: ["following"] });
      queryClient.invalidateQueries({ queryKey: ["followers"] });
    },
  });

  // 3. Block User Mutation
  const blockMutation = useMutation({
    mutationFn: async (userId: string) => {
      const res = await socialApi.blockUser(userId);
      return res.data;
    },
    onSuccess: () => {
      if (targetUsername) {
        const current = queryClient.getQueryData<UserProfile | null>(profileQueryKey);
        if (current) {
          queryClient.setQueryData<UserProfile>(profileQueryKey, {
            ...current,
            relationship: {
              isFollowing: false,
              isFollowedBy: false,
              isBlocked: true,
              isBlockedBy: false,
            },
          });
        }
        queryClient.invalidateQueries({ queryKey: profileQueryKey });
      }
      queryClient.invalidateQueries({ queryKey: ["followers"] });
      queryClient.invalidateQueries({ queryKey: ["following"] });
      success("User has been blocked.", "Account Blocked");
    },
    onError: () => {
      toastError("Failed to block user. Please try again.", "Error");
    },
  });

  // 4. Unblock User Mutation
  const unblockMutation = useMutation({
    mutationFn: async (userId: string) => {
      const res = await socialApi.unblockUser(userId);
      return res.data;
    },
    onSuccess: () => {
      if (targetUsername) {
        const current = queryClient.getQueryData<UserProfile | null>(profileQueryKey);
        if (current) {
          queryClient.setQueryData<UserProfile>(profileQueryKey, {
            ...current,
            relationship: {
              isFollowing: false,
              isFollowedBy: current.relationship?.isFollowedBy ?? false,
              isBlocked: false,
              isBlockedBy: false,
            },
          });
        }
        queryClient.invalidateQueries({ queryKey: profileQueryKey });
      }
      success("User has been unblocked.", "Account Unblocked");
    },
    onError: () => {
      toastError("Failed to unblock user. Please try again.", "Error");
    },
  });

  return {
    followUser: (id?: string) => followMutation.mutateAsync(id || targetUserId || ""),
    isFollowingLoading: followMutation.isPending,
    unfollowUser: (id?: string) => unfollowMutation.mutateAsync(id || targetUserId || ""),
    isUnfollowingLoading: unfollowMutation.isPending,
    blockUser: (id?: string) => blockMutation.mutateAsync(id || targetUserId || ""),
    isBlockingLoading: blockMutation.isPending,
    unblockUser: (id?: string) => unblockMutation.mutateAsync(id || targetUserId || ""),
    isUnblockingLoading: unblockMutation.isPending,
  };
}

export function useFollowers(targetUserId?: string, limit = 20) {
  return useInfiniteQuery({
    queryKey: ["followers", targetUserId],
    queryFn: async ({ pageParam }) => {
      if (!targetUserId) throw new Error("Target user ID required");
      const res = await socialApi.getFollowers(targetUserId, pageParam, limit);
      return res.data;
    },
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => (lastPage.hasNextPage ? lastPage.nextCursor : undefined),
    enabled: !!targetUserId,
  });
}

export function useFollowing(targetUserId?: string, limit = 20) {
  return useInfiniteQuery({
    queryKey: ["following", targetUserId],
    queryFn: async ({ pageParam }) => {
      if (!targetUserId) throw new Error("Target user ID required");
      const res = await socialApi.getFollowing(targetUserId, pageParam, limit);
      return res.data;
    },
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => (lastPage.hasNextPage ? lastPage.nextCursor : undefined),
    enabled: !!targetUserId,
  });
}
