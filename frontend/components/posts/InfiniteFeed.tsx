"use client";

import React, { useEffect, useRef, useMemo } from "react";
import { useInfiniteQuery, useQueryClient } from "@tanstack/react-query";
import { RefreshCw } from "lucide-react";
import { PostCard } from "./PostCard";
import { Button } from "../ui/button";
import type { Post } from "../../types/api";
import type { CursorPagination } from "../../lib/api/types";

export interface InfiniteFeedProps {
  queryKey: unknown[];
  queryFn: (cursor: string | null) => Promise<CursorPagination<Post>>;
  emptyTitle?: string;
  emptyDescription?: string;
  emptyAction?: React.ReactNode;
  onPostDeleted?: (postId: string) => void;
  headerSlot?: React.ReactNode;
  className?: string;
}

export function InfiniteFeed({
  queryKey,
  queryFn,
  emptyTitle = "Belum ada postingan, ikuti akun lain untuk mulai melihat linimasa.",
  emptyDescription = "Ikuti pembuat konten favorit Anda atau buat postingan pertama Anda untuk menghidupkan linimasa.",
  emptyAction,
  onPostDeleted,
  headerSlot,
  className = "",
}: InfiniteFeedProps) {
  const queryClient = useQueryClient();

  const {
    data,
    isLoading,
    isError,
    error,
    refetch,
    fetchNextPage,
    hasNextPage,
    isFetchingNextPage,
    isFetchNextPageError,
  } = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => queryFn(pageParam),
    initialPageParam: null as string | null,
    getNextPageParam: (lastPage) =>
      lastPage.hasNextPage ? (lastPage.nextCursor ?? undefined) : undefined,
  });

  // Defensive client-side deduplication across cursor pages
  const posts = useMemo(() => {
    if (!data?.pages) return [];
    const seen = new Set<string>();
    const list: Post[] = [];
    for (const page of data.pages) {
      if (!page?.items) continue;
      for (const post of page.items) {
        if (!seen.has(post.id)) {
          seen.add(post.id);
          list.push(post);
        }
      }
    }
    return list;
  }, [data]);

  // Intersection Observer for seamless auto-triggering on scroll near bottom
  const sentinelRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const el = sentinelRef.current;
    if (!el) return;

    const observer = new IntersectionObserver(
      (entries) => {
        const first = entries[0];
        if (first.isIntersecting && hasNextPage && !isFetchingNextPage) {
          fetchNextPage();
        }
      },
      { rootMargin: "250px" }
    );

    observer.observe(el);
    return () => observer.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const handleDeleted = (postId: string) => {
    queryClient.invalidateQueries({ queryKey });
    if (onPostDeleted) onPostDeleted(postId);
  };

  return (
    <div className={`space-y-4 ${className}`}>
      {headerSlot}

      {/* Initial Loading Skeleton State */}
      {isLoading && (
        <div
          className="space-y-4"
          role="status"
          aria-label="Loading posts"
          aria-live="polite"
        >
          {[1, 2, 3].map((i) => (
            <div
              key={i}
              className="rounded-2xl border border-border bg-card p-6 space-y-3 animate-pulse"
            >
              <div className="flex items-center space-x-3">
                <div className="h-10 w-10 rounded-full bg-muted" />
                <div className="space-y-1.5 flex-1">
                  <div className="h-4 w-32 rounded bg-muted" />
                  <div className="h-3 w-20 rounded bg-muted" />
                </div>
              </div>
              <div className="h-12 w-full rounded bg-muted" />
              <div className="h-4 w-2/3 rounded bg-muted/70" />
            </div>
          ))}
        </div>
      )}

      {/* Initial Load Error State */}
      {isError && (
        <div
          role="alert"
          className="rounded-2xl border border-destructive/20 bg-destructive/5 p-8 text-center space-y-3"
        >
          <p className="text-sm font-semibold text-destructive">
            Gagal memuat postingan
          </p>
          <p className="text-xs text-muted-foreground max-w-sm mx-auto">
            {(error as Error)?.message ||
              "Terjadi kesalahan saat memuat linimasa. Silakan coba lagi."}
          </p>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => refetch()}
            className="text-xs"
          >
            <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
            Coba Lagi
          </Button>
        </div>
      )}

      {/* Rendered Feed Posts */}
      {!isLoading && posts.length > 0 && (
        <div className="space-y-4" role="feed" aria-busy={isFetchingNextPage}>
          {posts.map((post) => (
            <PostCard key={post.id} post={post} onDeleted={handleDeleted} />
          ))}
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !isError && posts.length === 0 && (
        <div className="rounded-2xl border border-border bg-card p-8 text-center space-y-3">
          <p className="text-sm font-semibold text-foreground">{emptyTitle}</p>
          <p className="text-xs text-muted-foreground max-w-sm mx-auto">
            {emptyDescription}
          </p>
          {emptyAction && <div className="pt-2">{emptyAction}</div>}
        </div>
      )}

      {/* Recoverable Next-Page Error Retry State (Preserves previously loaded items) */}
      {isFetchNextPageError && (
        <div
          role="alert"
          className="rounded-2xl border border-destructive/20 bg-destructive/5 p-4 text-center space-y-2"
        >
          <p className="text-xs font-medium text-destructive">
            Gagal memuat halaman berikutnya
          </p>
          <Button
            type="button"
            size="sm"
            variant="outline"
            onClick={() => fetchNextPage()}
            className="text-xs"
          >
            <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
            Muat Ulang Halaman
          </Button>
        </div>
      )}

      {/* Next-Page Loading Indicator */}
      {isFetchingNextPage && (
        <div
          role="status"
          aria-live="polite"
          className="flex justify-center py-4"
        >
          <div className="flex items-center space-x-2 text-xs text-muted-foreground">
            <RefreshCw className="h-4 w-4 animate-spin text-primary" />
            <span>Memuat lebih banyak postingan...</span>
          </div>
        </div>
      )}

      {/* Infinite Scroll Sentinel */}
      <div
        ref={sentinelRef}
        className="h-1 w-full"
        aria-hidden="true"
        tabIndex={-1}
      />
    </div>
  );
}
