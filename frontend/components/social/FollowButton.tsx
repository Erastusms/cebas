"use client";

import React, { useState } from "react";
import { UserCheck, UserPlus, UserX } from "lucide-react";
import { Button } from "../ui/button";
import { useSocialGraph } from "../../hooks/useSocialGraph";
import { useAuth } from "../../hooks/useAuth";
import { cn } from "../../lib/utils";

export interface FollowButtonProps {
  targetUserId: string;
  targetUsername?: string;
  isFollowing?: boolean;
  isBlocked?: boolean;
  onFollowChange?: (isFollowing: boolean) => void;
  size?: "sm" | "md" | "lg";
  className?: string;
}

export function FollowButton({
  targetUserId,
  targetUsername,
  isFollowing = false,
  isBlocked = false,
  onFollowChange,
  size = "sm",
  className,
}: FollowButtonProps) {
  const { user: currentUser } = useAuth();
  const { followUser, unfollowUser, isFollowingLoading, isUnfollowingLoading } = useSocialGraph(
    targetUserId,
    targetUsername
  );
  const [isHovered, setIsHovered] = useState(false);

  // If user is viewing their own profile, do not render a follow button
  if (
    currentUser &&
    (currentUser.id === targetUserId ||
      (targetUsername && currentUser.username.toLowerCase() === targetUsername.toLowerCase()))
  ) {
    return null;
  }

  const isLoading = isFollowingLoading || isUnfollowingLoading;

  if (isBlocked) {
    return (
      <Button
        variant="outline"
        size={size}
        disabled
        className={cn("opacity-60 text-muted-foreground", className)}
        aria-label="Account is blocked"
      >
        <UserX className="h-3.5 w-3.5 mr-1.5" />
        Blocked
      </Button>
    );
  }

  const handleToggleFollow = async (e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();

    if (!currentUser) {
      // If unauthenticated, redirect or trigger auth notice
      window.location.href = "/login";
      return;
    }

    try {
      if (isFollowing) {
        onFollowChange?.(false);
        await unfollowUser(targetUserId);
      } else {
        onFollowChange?.(true);
        await followUser(targetUserId);
      }
    } catch {
      // Rollback is automatically handled in useSocialGraph hook
    }
  };

  if (isFollowing) {
    return (
      <Button
        variant={isHovered ? "destructive" : "outline"}
        size={size}
        isLoading={isLoading}
        onClick={handleToggleFollow}
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        className={cn(
          "font-medium transition-all min-w-[100px]",
          isHovered
            ? "border-destructive/40 bg-destructive/10 text-destructive hover:bg-destructive hover:text-destructive-foreground"
            : "border-border bg-card text-foreground hover:bg-muted",
          className
        )}
        aria-label={isHovered ? `Unfollow @${targetUsername || "user"}` : `Following @${targetUsername || "user"}`}
        aria-pressed={true}
      >
        {isHovered ? (
          <>
            <UserX className="h-3.5 w-3.5 mr-1.5" />
            Unfollow
          </>
        ) : (
          <>
            <UserCheck className="h-3.5 w-3.5 mr-1.5 text-primary" />
            Following
          </>
        )}
      </Button>
    );
  }

  return (
    <Button
      variant="default"
      size={size}
      isLoading={isLoading}
      onClick={handleToggleFollow}
      className={cn("font-medium min-w-[90px] shadow-sm", className)}
      aria-label={`Follow @${targetUsername || "user"}`}
      aria-pressed={false}
    >
      <UserPlus className="h-3.5 w-3.5 mr-1.5" />
      Follow
    </Button>
  );
}
