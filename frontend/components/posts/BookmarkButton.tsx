"use client";

import React, { useState, useEffect } from "react";
import { useRouter } from "next/navigation";
import { Bookmark } from "lucide-react";
import { useAuth } from "../../hooks/useAuth";
import { useBookmark } from "../../hooks/useEngagements";
import { useToast } from "../../hooks/useToast";

interface BookmarkButtonProps {
  postId: string;
  initialBookmarked: boolean;
  initialBookmarkCount?: number;
  className?: string;
  size?: "sm" | "md";
  showCount?: boolean;
}

export function BookmarkButton({
  postId,
  initialBookmarked,
  initialBookmarkCount = 0,
  className = "",
  size = "sm",
  showCount = false,
}: BookmarkButtonProps) {
  const router = useRouter();
  const { isAuthenticated } = useAuth();
  const { info: toastInfo } = useToast();
  const { toggleBookmark } = useBookmark(postId);
  const [isAnimating, setIsAnimating] = useState(false);

  // Local optimistic state for instant 0ms UI feedback
  const [bookmarked, setBookmarked] = useState(initialBookmarked);
  const [bookmarkCount, setBookmarkCount] = useState(initialBookmarkCount);

  useEffect(() => {
    setBookmarked(initialBookmarked);
  }, [initialBookmarked]);

  useEffect(() => {
    setBookmarkCount(initialBookmarkCount);
  }, [initialBookmarkCount]);

  const handleClick = (e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();

    if (!isAuthenticated) {
      toastInfo("Please log in to bookmark posts.", "Authentication Required");
      router.push("/login");
      return;
    }

    const currentBookmarked = bookmarked;
    const nextBookmarked = !currentBookmarked;
    const nextCount = nextBookmarked ? bookmarkCount + 1 : Math.max(0, bookmarkCount - 1);

    setBookmarked(nextBookmarked);
    setBookmarkCount(nextCount);
    setIsAnimating(true);

    toggleBookmark(currentBookmarked);
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
      aria-label={bookmarked ? "Remove bookmark" : "Bookmark post"}
      aria-pressed={bookmarked}
      className={`group flex items-center space-x-1.5 rounded-md px-2 py-1 transition focus:outline-none focus-visible:ring-2 focus-visible:ring-primary/50 ${
        bookmarked
          ? "text-primary hover:bg-primary/10"
          : "text-muted-foreground hover:bg-muted hover:text-primary"
      } ${className}`}
    >
      <Bookmark
        className={`${iconSizes[size]} transition-all duration-200 ${
          bookmarked
            ? "fill-primary text-primary"
            : "group-hover:scale-110"
        } ${isAnimating ? "scale-125" : "scale-100"}`}
      />
      {showCount && (
        <span
          className={`${textSizes[size]} font-medium tabular-nums ${
            bookmarked ? "text-primary" : ""
          }`}
        >
          {bookmarkCount}
        </span>
      )}
    </button>
  );
}
