"use client";

import React, { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { Heart } from "lucide-react";
import { useAuth } from "../../hooks/useAuth";
import { useLike } from "../../hooks/useEngagements";
import { useToast } from "../../hooks/useToast";

interface LikeButtonProps {
  postId: string;
  initialLiked: boolean;
  initialLikeCount: number;
  className?: string;
  size?: "sm" | "md";
}

export function LikeButton({
  postId,
  initialLiked,
  initialLikeCount,
  className = "",
  size = "sm",
}: LikeButtonProps) {
  const router = useRouter();
  const { isAuthenticated } = useAuth();
  const { info: toastInfo } = useToast();
  const { toggleLike } = useLike(postId);
  const [isAnimating, setIsAnimating] = useState(false);

  // Local optimistic state for instant 0ms UI feedback
  const [liked, setLiked] = useState(initialLiked);
  const [likeCount, setLikeCount] = useState(initialLikeCount);

  useEffect(() => {
    setLiked(initialLiked);
  }, [initialLiked]);

  useEffect(() => {
    setLikeCount(initialLikeCount);
  }, [initialLikeCount]);

  const handleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();

    if (!isAuthenticated) {
      toastInfo("Please log in to like posts.", "Authentication Required");
      router.push("/login");
      return;
    }

    const currentLiked = liked;
    const nextLiked = !currentLiked;
    const nextCount = nextLiked ? likeCount + 1 : Math.max(0, likeCount - 1);

    setLiked(nextLiked);
    setLikeCount(nextCount);
    setIsAnimating(true);

    toggleLike(currentLiked);
    setTimeout(() => setIsAnimating(false), 300);
  };

  const iconSizes = {
    sm: "h-4 w-4",
    md: "h-5 w-5",
  };

  const textSizes = {
    sm: "text-xs",
    md: "text-sm",
  };

  return (
    <button
      type="button"
      onClick={handleClick}
      aria-label={liked ? "Unlike post" : "Like post"}
      aria-pressed={liked}
      className={`group flex items-center space-x-1.5 rounded-md px-2 py-1 transition focus:outline-none focus-visible:ring-2 focus-visible:ring-red-500/50 ${
        liked
          ? "text-red-500 hover:bg-red-500/10 dark:text-red-400"
          : "text-muted-foreground hover:bg-muted hover:text-red-500"
      } ${className}`}
    >
      <Heart
        className={`${iconSizes[size]} transition-all duration-200 ${
          liked
            ? "fill-red-500 text-red-500 dark:fill-red-400 dark:text-red-400"
            : "group-hover:scale-110"
        } ${isAnimating ? "scale-125" : "scale-100"}`}
      />
      <span
        className={`${textSizes[size]} font-medium tabular-nums ${
          liked ? "text-red-500 dark:text-red-400" : ""
        }`}
      >
        {likeCount}
      </span>
    </button>
  );
}
