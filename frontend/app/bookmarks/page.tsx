"use client";

import React from "react";
import Link from "next/link";
import { Bookmark, RefreshCw, Sparkles } from "lucide-react";
import { useBookmarks } from "../../hooks/useEngagements";
import { AuthGuard } from "../../components/auth/AuthGuard";
import { PostCard } from "../../components/posts/PostCard";
import { Button } from "../../components/ui/button";
import { Skeleton } from "../../components/ui/skeleton";
import type { BookmarkedPost } from "../../types/api";

export default function BookmarksPage() {
  const {
    data,
    isLoading,
    isError,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    refetch,
  } = useBookmarks(20);

  const rawItems = data?.pages.flatMap((page) => page.items) ?? [];
  const seenIds = new Set<string>();
  const allBookmarks: BookmarkedPost[] = rawItems
    .map((item: BookmarkedPost) => ({
      ...item,
      id: item.id || item.postId || item.bookmarkId,
    }))
    .filter((item: BookmarkedPost) => {
      const stableId = item.id || item.postId || item.bookmarkId;
      if (!stableId || seenIds.has(stableId)) return false;
      seenIds.add(stableId);
      return true;
    });

  return (
    <AuthGuard>
      <main className="max-w-2xl mx-auto px-4 py-6 sm:py-8 space-y-6">
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border/80 pb-4">
          <div className="flex items-center space-x-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Bookmark className="h-5 w-5 fill-primary" />
            </div>
            <div>
              <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-foreground">
                Bookmarks
              </h1>
              <p className="text-xs sm:text-sm text-muted-foreground">
                Your private collection of saved posts
              </p>
            </div>
          </div>

          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            className="text-xs"
            aria-label="Refresh bookmarks"
          >
            <RefreshCw className="h-3.5 w-3.5 mr-1.5" />
            <span>Refresh</span>
          </Button>
        </div>

        {/* Content Section */}
        {isLoading ? (
          <div className="space-y-4">
            <Skeleton className="h-32 w-full rounded-2xl" />
            <Skeleton className="h-44 w-full rounded-2xl" />
            <Skeleton className="h-36 w-full rounded-2xl" />
          </div>
        ) : isError ? (
          <div className="rounded-2xl border border-destructive/30 bg-destructive/10 p-6 text-center space-y-3">
            <p className="text-sm font-medium text-destructive">
              Failed to load bookmarks. Please try again.
            </p>
            <Button
              variant="outline"
              size="sm"
              onClick={() => refetch()}
              className="text-xs"
            >
              Try Again
            </Button>
          </div>
        ) : allBookmarks.length === 0 ? (
          /* Empty State */
          <div className="rounded-2xl border border-border bg-card p-12 text-center space-y-4 shadow-sm">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-muted text-muted-foreground">
              <Bookmark className="h-7 w-7" />
            </div>
            <div className="space-y-1.5">
              <h3 className="text-base font-semibold text-foreground">
                No bookmarks yet.
              </h3>
              <p className="text-xs sm:text-sm text-muted-foreground max-w-sm mx-auto">
                Save posts to find them here later. Click the bookmark icon on any post to add it to your library.
              </p>
            </div>
            <div className="pt-2">
              <Link href="/">
                <Button size="sm" variant="default" className="text-xs">
                  <Sparkles className="h-3.5 w-3.5 mr-1.5" />
                  Explore Feed
                </Button>
              </Link>
            </div>
          </div>
        ) : (
          /* Bookmarks List */
          <div className="space-y-4">
            {allBookmarks.map((bookmarkedPost) => (
              <PostCard
                key={bookmarkedPost.id || bookmarkedPost.postId || bookmarkedPost.bookmarkId}
                post={bookmarkedPost}
              />
            ))}

            {/* Pagination Load More */}
            {hasNextPage && (
              <div className="pt-4 text-center">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => fetchNextPage()}
                  disabled={isFetchingNextPage}
                  className="text-xs"
                >
                  {isFetchingNextPage ? "Loading more..." : "Load More Bookmarks"}
                </Button>
              </div>
            )}
          </div>
        )}
      </main>
    </AuthGuard>
  );
}
