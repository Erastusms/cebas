"use client";

import React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { Heart, MessageSquare, UserPlus, Bell, Check } from "lucide-react";
import { formatPostTimestamp } from "../../lib/utils/time";
import { useMarkNotificationRead } from "../../hooks/useNotifications";
import type { NotificationItem as NotificationItemType } from "../../types/api";

interface NotificationItemProps {
  notification: NotificationItemType;
}

export function NotificationItem({ notification }: NotificationItemProps) {
  const router = useRouter();
  const markReadMutation = useMarkNotificationRead();

  const handleClick = (e: React.MouseEvent) => {
    // If clicking on a direct link inside, don't trigger wrapper
    if ((e.target as HTMLElement).closest("a")) return;

    if (!notification.isRead) {
      markReadMutation.mutate(notification.id);
    }

    if (notification.type === "USER_FOLLOWED") {
      router.push(`/user/${encodeURIComponent(notification.actor.username)}`);
    } else if (notification.targetId) {
      router.push(`/post/${encodeURIComponent(notification.targetId)}`);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === "Enter" || e.key === " ") {
      e.preventDefault();
      handleClick(e as unknown as React.MouseEvent);
    }
  };

  const getNotificationIcon = () => {
    switch (notification.type) {
      case "POST_LIKED":
      case "REPLY_LIKED":
        return (
          <div className="flex h-7 w-7 items-center justify-center rounded-full bg-rose-500/10 text-rose-500">
            <Heart className="h-4 w-4 fill-rose-500" />
          </div>
        );
      case "POST_REPLIED":
        return (
          <div className="flex h-7 w-7 items-center justify-center rounded-full bg-sky-500/10 text-sky-500">
            <MessageSquare className="h-4 w-4 fill-sky-500" />
          </div>
        );
      case "USER_FOLLOWED":
        return (
          <div className="flex h-7 w-7 items-center justify-center rounded-full bg-emerald-500/10 text-emerald-500">
            <UserPlus className="h-4 w-4" />
          </div>
        );
      default:
        return (
          <div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary/10 text-primary">
            <Bell className="h-4 w-4" />
          </div>
        );
    }
  };

  const getActionDescription = () => {
    switch (notification.type) {
      case "POST_LIKED":
        return "menyukai celotehan Anda";
      case "POST_REPLIED":
        return "membalas celotehan Anda";
      case "REPLY_LIKED":
        return "menyukai balasan Anda";
      case "USER_FOLLOWED":
        return "mulai mengikuti Anda";
      default:
        return "berinteraksi dengan Anda";
    }
  };

  return (
    <div
      role="button"
      tabIndex={0}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      className={`group relative flex items-start space-x-3.5 rounded-2xl border p-4 transition cursor-pointer select-none ${
        notification.isRead
          ? "border-border/60 bg-card hover:bg-muted/40"
          : "border-primary/20 bg-primary/[0.03] hover:bg-primary/[0.06]"
      }`}
    >
      {/* Icon Badge */}
      <div className="flex-shrink-0 pt-0.5">{getNotificationIcon()}</div>

      {/* Main Content */}
      <div className="flex-1 min-w-0 space-y-1">
        <div className="flex items-center space-x-2">
          {/* Actor Avatar */}
          <Link
            href={`/user/${encodeURIComponent(notification.actor.username)}`}
            className="flex-shrink-0 overflow-hidden rounded-full border border-border"
            onClick={(e) => e.stopPropagation()}
          >
            <div className="flex h-6 w-6 items-center justify-center rounded-full bg-muted text-[11px] font-bold text-foreground">
              {notification.actor.avatarUrl ? (
                <img
                  src={notification.actor.avatarUrl}
                  alt={notification.actor.displayName || notification.actor.username}
                  className="h-full w-full object-cover"
                />
              ) : (
                notification.actor.displayName?.charAt(0).toUpperCase() ||
                notification.actor.username.charAt(0).toUpperCase()
              )}
            </div>
          </Link>

          {/* Actor Names & Action */}
          <div className="truncate text-xs sm:text-sm">
            <Link
              href={`/user/${encodeURIComponent(notification.actor.username)}`}
              className="font-bold text-foreground hover:underline mr-1"
              onClick={(e) => e.stopPropagation()}
            >
              {notification.actor.displayName || notification.actor.username}
            </Link>
            <span className="text-muted-foreground">{getActionDescription()}</span>
          </div>
        </div>

        {/* Timestamp */}
        <div className="flex items-center space-x-2 text-[11px] text-muted-foreground">
          <span>{formatPostTimestamp(notification.createdAt)}</span>
          {notification.isRead && (
            <span className="flex items-center text-muted-foreground/80">
              <Check className="h-3 w-3 mr-0.5 text-emerald-500" />
              Sudah dibaca
            </span>
          )}
        </div>
      </div>

      {/* Unread Indicator Dot */}
      {!notification.isRead && (
        <div
          title="Belum dibaca"
          className="flex h-2.5 w-2.5 flex-shrink-0 rounded-full bg-primary mt-1.5 shadow-sm"
        />
      )}
    </div>
  );
}
