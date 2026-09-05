"use client";

import { useEffect } from "react";
import {
  useQuery,
  useInfiniteQuery,
  useMutation,
  useQueryClient,
  type InfiniteData,
} from "@tanstack/react-query";
import { notificationsApi } from "../lib/api/notifications";
import { useAuth } from "./useAuth";
import { useRealtimeEvent } from "./useRealtime";
import { socialHubClient } from "../lib/realtime/socialHubClient";
import { useToast } from "./useToast";
import type {
  NotificationItem,
  NotificationCreatedEvent,
  ApiResponse,
  CursorPagination,
} from "../types/api";

const NOTIFICATIONS_QUERY_KEY = ["notifications", "list"] as const;
const UNREAD_COUNT_QUERY_KEY = ["notifications", "unread-count"] as const;

/**
 * Hook to retrieve and subscribe to unread notification count.
 * Handles instant optimistic increment on real-time event and fallback polling when disconnected.
 */
export function useUnreadNotificationCount() {
  const { user } = useAuth();
  const queryClient = useQueryClient();
  const { info } = useToast();

  const query = useQuery({
    queryKey: UNREAD_COUNT_QUERY_KEY,
    queryFn: async () => {
      const res = await notificationsApi.getUnreadCount();
      return res.data;
    },
    enabled: !!user?.id,
    staleTime: 30 * 1000,
  });

  // Register fallback polling callback when disconnected
  useEffect(() => {
    if (!user?.id) return;
    const unsub = socialHubClient.onFallbackPoll(() => {
      queryClient.invalidateQueries({ queryKey: UNREAD_COUNT_QUERY_KEY });
    });
    return unsub;
  }, [user?.id, queryClient]);

  // Real-time listener for instant badge counter increment
  useRealtimeEvent("NotificationReceived", (event: NotificationCreatedEvent) => {
    queryClient.setQueryData(
      UNREAD_COUNT_QUERY_KEY,
      (old: { unreadCount: number } | undefined) => {
        const current = old?.unreadCount ?? 0;
        return { unreadCount: current + 1 };
      }
    );

    // Also invalidate the notifications list so the latest item appears
    queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_QUERY_KEY });

    // Show toast for new notification
    let message = "Notifikasi baru diterima.";
    switch (event.type) {
      case "POST_LIKED":
        message = "Seseorang menyukai celotehan Anda.";
        break;
      case "POST_REPLIED":
        message = "Seseorang membalas celotehan Anda.";
        break;
      case "REPLY_LIKED":
        message = "Seseorang menyukai balasan Anda.";
        break;
      case "USER_FOLLOWED":
        message = "Seseorang mulai mengikuti Anda.";
        break;
    }
    info(message, "Notifikasi");
  });

  return {
    unreadCount: query.data?.unreadCount ?? 0,
    isLoading: query.isLoading,
    refetch: query.refetch,
  };
}

/**
 * Hook to retrieve cursor-paginated notifications with unread filtering.
 */
export function useNotifications(unreadOnly = false) {
  const { user } = useAuth();
  const queryClient = useQueryClient();

  const query = useInfiniteQuery({
    queryKey: [...NOTIFICATIONS_QUERY_KEY, { unreadOnly }],
    queryFn: async ({ pageParam }: { pageParam?: string | null }) => {
      const res = await notificationsApi.getNotifications({
        cursor: pageParam,
        limit: 20,
        unreadOnly,
      });
      return res.data;
    },
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) =>
      lastPage.hasNextPage && lastPage.nextCursor ? lastPage.nextCursor : undefined,
    enabled: !!user?.id,
    staleTime: 15 * 1000,
  });

  // Fallback polling for list when disconnected
  useEffect(() => {
    if (!user?.id) return;
    const unsub = socialHubClient.onFallbackPoll(() => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_QUERY_KEY });
    });
    return unsub;
  }, [user?.id, queryClient]);

  const notifications = query.data?.pages.flatMap((page) => page.items) ?? [];

  return {
    notifications,
    isLoading: query.isLoading,
    isFetchingNextPage: query.isFetchingNextPage,
    hasNextPage: !!query.hasNextPage,
    fetchNextPage: query.fetchNextPage,
    refetch: query.refetch,
  };
}

/**
 * Hook to mark a single notification as read with optimistic UI update.
 */
export function useMarkNotificationRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (notificationId: string) => {
      const res = await notificationsApi.markAsRead(notificationId);
      return res.data;
    },
    onMutate: async (notificationId) => {
      // Cancel outgoing queries
      await queryClient.cancelQueries({ queryKey: NOTIFICATIONS_QUERY_KEY });
      await queryClient.cancelQueries({ queryKey: UNREAD_COUNT_QUERY_KEY });

      // Optimistically decrement unread count
      queryClient.setQueryData(
        UNREAD_COUNT_QUERY_KEY,
        (old: { unreadCount: number } | undefined) => {
          if (!old) return old;
          return { unreadCount: Math.max(0, old.unreadCount - 1) };
        }
      );

      // Optimistically update notifications list
      queryClient.setQueriesData<InfiniteData<CursorPagination<NotificationItem>>>(
        { queryKey: NOTIFICATIONS_QUERY_KEY },
        (oldData) => {
          if (!oldData) return oldData;
          return {
            ...oldData,
            pages: oldData.pages.map((page) => ({
              ...page,
              items: page.items.map((item) =>
                item.id === notificationId
                  ? { ...item, isRead: true, readAt: new Date().toISOString() }
                  : item
              ),
            })),
          };
        }
      );
    },
    onError: () => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_QUERY_KEY });
      queryClient.invalidateQueries({ queryKey: UNREAD_COUNT_QUERY_KEY });
    },
  });
}

/**
 * Hook to mark all notifications as read with optimistic UI update.
 */
export function useMarkAllNotificationsRead() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async () => {
      const res = await notificationsApi.markAllAsRead();
      return res.data;
    },
    onMutate: async () => {
      await queryClient.cancelQueries({ queryKey: NOTIFICATIONS_QUERY_KEY });
      await queryClient.cancelQueries({ queryKey: UNREAD_COUNT_QUERY_KEY });

      // Optimistically clear unread count
      queryClient.setQueryData(UNREAD_COUNT_QUERY_KEY, { unreadCount: 0 });

      // Optimistically mark all items as read
      queryClient.setQueriesData<InfiniteData<CursorPagination<NotificationItem>>>(
        { queryKey: NOTIFICATIONS_QUERY_KEY },
        (oldData) => {
          if (!oldData) return oldData;
          return {
            ...oldData,
            pages: oldData.pages.map((page) => ({
              ...page,
              items: page.items.map((item) => ({
                ...item,
                isRead: true,
                readAt: item.readAt ?? new Date().toISOString(),
              })),
            })),
          };
        }
      );
    },
    onError: () => {
      queryClient.invalidateQueries({ queryKey: NOTIFICATIONS_QUERY_KEY });
      queryClient.invalidateQueries({ queryKey: UNREAD_COUNT_QUERY_KEY });
    },
  });
}
