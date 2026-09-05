"use client";

import React, { useState } from "react";
import { Bell, CheckCheck, RefreshCw } from "lucide-react";
import {
  useNotifications,
  useUnreadNotificationCount,
  useMarkAllNotificationsRead,
} from "../../hooks/useNotifications";
import { NotificationItem } from "./NotificationItem";
import { Button } from "../ui/button";
import { Skeleton } from "../ui/skeleton";

export function NotificationList() {
  const [unreadOnly, setUnreadOnly] = useState(false);
  const {
    notifications,
    isLoading,
    isFetchingNextPage,
    hasNextPage,
    fetchNextPage,
    refetch,
  } = useNotifications(unreadOnly);

  const { unreadCount } = useUnreadNotificationCount();
  const markAllReadMutation = useMarkAllNotificationsRead();

  // Deduplicate notifications by id to guarantee zero duplicates across pages or real-time inserts
  const seenIds = new Set<string>();
  const deduplicatedNotifications = notifications.filter((item) => {
    if (seenIds.has(item.id)) return false;
    seenIds.add(item.id);
    return true;
  });

  return (
    <div className="space-y-6">
      {/* Header Controls: Filter Tabs & Mark All As Read */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 border-b border-border/80 pb-4">
        {/* Filter Tabs */}
        <div className="flex items-center space-x-1 rounded-xl bg-muted/60 p-1">
          <button
            type="button"
            onClick={() => setUnreadOnly(false)}
            className={`flex items-center space-x-1.5 rounded-lg px-3.5 py-1.5 text-xs font-semibold transition ${
              !unreadOnly
                ? "bg-card text-foreground shadow-sm"
                : "text-muted-foreground hover:text-foreground"
            }`}
          >
            <span>Semua</span>
          </button>
          <button
            type="button"
            onClick={() => setUnreadOnly(true)}
            className={`flex items-center space-x-1.5 rounded-lg px-3.5 py-1.5 text-xs font-semibold transition ${
              unreadOnly
                ? "bg-card text-foreground shadow-sm"
                : "text-muted-foreground hover:text-foreground"
            }`}
          >
            <span>Belum Dibaca</span>
            {unreadCount > 0 && (
              <span className="rounded-full bg-primary/20 px-1.5 py-0.2 text-[10px] font-bold text-primary">
                {unreadCount}
              </span>
            )}
          </button>
        </div>

        {/* Action Buttons */}
        <div className="flex items-center space-x-2 self-end sm:self-auto">
          <Button
            variant="outline"
            size="sm"
            onClick={() => markAllReadMutation.mutate()}
            disabled={unreadCount === 0 || markAllReadMutation.isPending}
            className="text-xs"
            aria-label="Tandai semua telah dibaca"
          >
            <CheckCheck className="h-3.5 w-3.5 mr-1.5" />
            <span>Tandai Semua Dibaca</span>
          </Button>

          <Button
            variant="ghost"
            size="sm"
            onClick={() => refetch()}
            className="text-xs text-muted-foreground hover:text-foreground"
            aria-label="Segarkan notifikasi"
          >
            <RefreshCw className="h-3.5 w-3.5" />
          </Button>
        </div>
      </div>

      {/* Content Section */}
      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-20 w-full rounded-2xl" />
          <Skeleton className="h-20 w-full rounded-2xl" />
          <Skeleton className="h-20 w-full rounded-2xl" />
          <Skeleton className="h-20 w-full rounded-2xl" />
        </div>
      ) : deduplicatedNotifications.length === 0 ? (
        /* Empty State */
        <div className="rounded-2xl border border-border bg-card p-12 text-center space-y-3 shadow-sm">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-muted text-muted-foreground">
            <Bell className="h-6 w-6" />
          </div>
          <div className="space-y-1">
            <h3 className="text-base font-semibold text-foreground">
              {unreadOnly ? "Tidak ada notifikasi baru" : "Belum ada notifikasi"}
            </h3>
            <p className="text-xs sm:text-sm text-muted-foreground max-w-sm mx-auto">
              {unreadOnly
                ? "Semua notifikasi Anda sudah dibaca. Anda sudah mengetahui kabar terbaru!"
                : "Ketika pengguna lain menyukai, membalas celotehan, atau mulai mengikuti Anda, notifikasi akan muncul di sini."}
            </p>
          </div>
        </div>
      ) : (
        /* Notifications Feed */
        <div className="space-y-2.5">
          {deduplicatedNotifications.map((notif) => (
            <NotificationItem key={notif.id} notification={notif} />
          ))}

          {/* Load More Pagination */}
          {hasNextPage && (
            <div className="pt-4 text-center">
              <Button
                variant="outline"
                size="sm"
                onClick={() => fetchNextPage()}
                disabled={isFetchingNextPage}
                className="text-xs"
              >
                {isFetchingNextPage ? "Memuat lebih banyak..." : "Muat Notifikasi Lainnya"}
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
