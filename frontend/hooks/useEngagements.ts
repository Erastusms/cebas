"use client";

import { useMutation, useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { engagementsApi } from "../lib/api/engagements";
import type { Post, CursorPagination, BookmarkedPost } from "../types/api";
import { useToast } from "./useToast";

/**
 * Helper to update a Post entity across all active React Query cache entries
 */
function updatePostInAllCaches(
  queryClient: ReturnType<typeof useQueryClient>,
  postId: string,
  updater: (prev: Post) => Post
) {
  // Update ANY query in the cache that holds this post (single, list, or infinite)
  queryClient.setQueriesData(
    {
      predicate: (query) => {
        const data: any = query.state.data;
        if (!data) return false;
        if (data.id === postId) return true;
        if (Array.isArray(data.items) && data.items.some((item: any) => item && item.id === postId)) return true;
        if (Array.isArray(data.pages) && data.pages.some((page: any) => page && Array.isArray(page.items) && page.items.some((item: any) => item && item.id === postId))) return true;
        return false;
      },
    },
    (oldData: any) => {
      if (!oldData) return oldData;

      // Case 1: Single post detail (e.g. ["post-detail", postId])
      if (oldData.id === postId) {
        return updater(oldData);
      }

      // Case 2: Standard paginated list { items: Post[] } (e.g. ["timeline-posts", ...], ["user-posts", ...])
      if (Array.isArray(oldData.items)) {
        return {
          ...oldData,
          items: oldData.items.map((item: any) => (item.id === postId ? updater(item) : item)),
        };
      }

      // Case 3: Infinite query { pages: Array<{ items: Post[] }> } (e.g. ["bookmarks"])
      if (Array.isArray(oldData.pages)) {
        return {
          ...oldData,
          pages: oldData.pages.map((page: any) => {
            if (!page || !Array.isArray(page.items)) return page;
            return {
              ...page,
              items: page.items.map((item: any) => (item.id === postId ? updater(item) : item)),
            };
          }),
        };
      }

      return oldData;
    }
  );
}

export function useLike(postId: string) {
  const queryClient = useQueryClient();
  const { error: toastError } = useToast();

  const likeMutation = useMutation({
    mutationFn: async (currentlyLiked: boolean) => {
      if (currentlyLiked) {
        const res = await engagementsApi.removeLike(postId);
        return res.data;
      } else {
        const res = await engagementsApi.createLike(postId);
        return res.data;
      }
    },
    onMutate: async (currentlyLiked: boolean) => {
      // Cancel queries to prevent background refetches from overwriting optimistic state
      await queryClient.cancelQueries({
        predicate: (query) => {
          const first = String(query.queryKey[0]);
          return (
            first === "post-detail" ||
            first === "timeline-posts" ||
            first === "user-posts"
          );
        },
      });

      // Optimistically update caches
      updatePostInAllCaches(queryClient, postId, (post) => ({
        ...post,
        liked: !currentlyLiked,
        likeCount: currentlyLiked
          ? Math.max(0, post.likeCount - 1)
          : post.likeCount + 1,
      }));

      return { previousLiked: currentlyLiked };
    },
    onSuccess: (data) => {
      // Reconcile with authoritative server state
      updatePostInAllCaches(queryClient, postId, (post) => ({
        ...post,
        liked: data.liked,
        likeCount: data.likeCount,
      }));
    },
    onError: (_err, currentlyLiked) => {
      // Rollback to previous state
      updatePostInAllCaches(queryClient, postId, (post) => ({
        ...post,
        liked: currentlyLiked,
        likeCount: currentlyLiked
          ? post.likeCount + 1
          : Math.max(0, post.likeCount - 1),
      }));
      toastError("Failed to update like. Please try again.", "Error");
    },
    onSettled: () => {
      queryClient.invalidateQueries({
        predicate: (query) => {
          const first = String(query.queryKey[0]);
          return (
            first === "post-detail" ||
            first === "timeline-posts" ||
            first === "user-posts" ||
            first === "bookmarks"
          );
        },
      });
    },
  });

  return {
    toggleLike: (currentlyLiked: boolean) => likeMutation.mutate(currentlyLiked),
    isLikePending: likeMutation.isPending,
  };
}

export function useBookmark(postId: string) {
  const queryClient = useQueryClient();
  const { success: toastSuccess, info: toastInfo, error: toastError } = useToast();

  const bookmarkMutation = useMutation({
    mutationFn: async (currentlyBookmarked: boolean) => {
      if (currentlyBookmarked) {
        const res = await engagementsApi.removeBookmark(postId);
        return res.data;
      } else {
        const res = await engagementsApi.createBookmark(postId);
        return res.data;
      }
    },
    onMutate: async (currentlyBookmarked: boolean) => {
      await queryClient.cancelQueries({
        predicate: (query) => {
          const first = String(query.queryKey[0]);
          return (
            first === "post-detail" ||
            first === "timeline-posts" ||
            first === "user-posts" ||
            first === "bookmarks"
          );
        },
      });

      // Optimistically update caches
      updatePostInAllCaches(queryClient, postId, (post) => ({
        ...post,
        bookmarked: !currentlyBookmarked,
        bookmarkCount: currentlyBookmarked
          ? Math.max(0, post.bookmarkCount - 1)
          : post.bookmarkCount + 1,
      }));

      return { previousBookmarked: currentlyBookmarked };
    },
    onSuccess: (data) => {
      // Reconcile with authoritative server state
      updatePostInAllCaches(queryClient, postId, (post) => ({
        ...post,
        bookmarked: data.bookmarked,
        bookmarkCount: data.bookmarkCount,
      }));

      if (data.bookmarked) {
        toastSuccess("Post saved to bookmarks", "Saved");
      } else {
        toastInfo("Post removed from bookmarks", "Removed");
      }
    },
    onError: (_err, currentlyBookmarked) => {
      // Rollback
      updatePostInAllCaches(queryClient, postId, (post) => ({
        ...post,
        bookmarked: currentlyBookmarked,
        bookmarkCount: currentlyBookmarked
          ? post.bookmarkCount + 1
          : Math.max(0, post.bookmarkCount - 1),
      }));
      toastError("Failed to update bookmark. Please try again.", "Error");
    },
    onSettled: () => {
      queryClient.invalidateQueries({
        predicate: (query) => {
          const first = String(query.queryKey[0]);
          return (
            first === "post-detail" ||
            first === "timeline-posts" ||
            first === "user-posts" ||
            first === "bookmarks"
          );
        },
      });
    },
  });

  return {
    toggleBookmark: (currentlyBookmarked: boolean) => bookmarkMutation.mutate(currentlyBookmarked),
    isBookmarkPending: bookmarkMutation.isPending,
  };
}

export function useBookmarks(limit = 20) {
  return useInfiniteQuery({
    queryKey: ["bookmarks"],
    queryFn: async ({ pageParam }) => {
      const res = await engagementsApi.getBookmarks(pageParam, limit);
      return res.data;
    },
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) => (lastPage.hasNextPage ? lastPage.nextCursor : undefined),
  });
}
