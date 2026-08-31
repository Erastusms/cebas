"use client";

import React from "react";
import { cn } from "../../lib/utils";

export interface FollowStatusBadgeProps {
  isFollowing?: boolean;
  isFollowedBy?: boolean;
  isBlocked?: boolean;
  className?: string;
}

export function FollowStatusBadge({
  isFollowing = false,
  isFollowedBy = false,
  isBlocked = false,
  className,
}: FollowStatusBadgeProps) {
  if (isBlocked) {
    return (
      <span
        className={cn(
          "inline-flex items-center rounded-md px-2 py-0.5 text-[11px] font-medium bg-destructive/10 text-destructive border border-destructive/20 select-none",
          className
        )}
      >
        Blocked
      </span>
    );
  }

  if (isFollowing && isFollowedBy) {
    return (
      <span
        className={cn(
          "inline-flex items-center rounded-md px-2 py-0.5 text-[11px] font-medium bg-primary/10 text-primary border border-primary/20 select-none",
          className
        )}
      >
        Mutual
      </span>
    );
  }

  if (isFollowedBy) {
    return (
      <span
        className={cn(
          "inline-flex items-center rounded-md px-2 py-0.5 text-[11px] font-medium bg-muted text-muted-foreground border border-border select-none",
          className
        )}
      >
        Follows you
      </span>
    );
  }

  return null;
}
