"use client";

import React from "react";
import Link from "next/link";
import { useQuery } from "@tanstack/react-query";
import { Flame, RefreshCw, TrendingUp } from "lucide-react";
import { trendsApi, TrendingTopic } from "../../lib/api/trends";
import { Button } from "../ui/button";

interface TrendingWidgetProps {
  className?: string;
  limit?: number;
}

export function TrendingWidget({ className = "", limit = 10 }: TrendingWidgetProps) {
  const {
    data,
    isLoading,
    isError,
    refetch,
    isFetching,
  } = useQuery({
    queryKey: ["trending-topics", limit],
    queryFn: async () => {
      const res = await trendsApi.getTrends(limit);
      return res.data;
    },
    staleTime: 30 * 1000, // 30 seconds
    refetchOnWindowFocus: false,
  });

  const trends: TrendingTopic[] = data?.items ?? [];

  return (
    <aside
      aria-label="Tren untuk Anda"
      className={`rounded-2xl border border-border bg-card p-4 sm:p-5 shadow-sm transition ${className}`}
    >
      {/* Widget Header */}
      <div className="flex items-center justify-between border-b border-border/80 pb-3 mb-3">
        <div className="flex items-center space-x-2">
          <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-primary/10 text-primary">
            <Flame className="h-4 w-4" />
          </div>
          <div>
            <h2 className="text-sm font-bold text-foreground tracking-tight">
              Tren untuk Anda
            </h2>
            <p className="text-[11px] text-muted-foreground">Topik hangat di CEBAS</p>
          </div>
        </div>

        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={() => refetch()}
          disabled={isFetching}
          className="h-7 w-7 p-0 text-muted-foreground hover:text-foreground rounded-full"
          aria-label="Segarkan tren"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${isFetching ? "animate-spin text-primary" : ""}`} />
        </Button>
      </div>

      {/* Loading Skeleton */}
      {isLoading && (
        <div className="space-y-3 py-1" aria-busy="true" aria-label="Memuat tren">
          {[1, 2, 3, 4, 5].map((i) => (
            <div key={i} className="flex items-center space-x-3">
              <div className="h-4 w-6 rounded bg-muted/40 animate-pulse" />
              <div className="flex-1 space-y-1.5">
                <div className="h-4 w-28 rounded bg-muted/40 animate-pulse" />
                <div className="h-3 w-16 rounded bg-muted/30 animate-pulse" />
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Error State */}
      {isError && (
        <div className="py-4 text-center space-y-2">
          <p className="text-xs text-muted-foreground">Gagal memuat tren saat ini.</p>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            className="text-xs h-7 px-2.5"
          >
            Coba Lagi
          </Button>
        </div>
      )}

      {/* Empty State */}
      {!isLoading && !isError && trends.length === 0 && (
        <div className="py-6 text-center text-xs text-muted-foreground space-y-1">
          <TrendingUp className="h-6 w-6 mx-auto text-muted-foreground/50 mb-1" />
          <p>Belum ada topik tren saat ini.</p>
          <p className="text-[11px] text-muted-foreground/70">
            Gunakan tanda pagar di celotehan Anda untuk memulai percakapan baru.
          </p>
        </div>
      )}

      {/* Trends List */}
      {!isLoading && !isError && trends.length > 0 && (
        <nav aria-label="Daftar tren" className="space-y-1">
          {trends.map((topic) => (
            <Link
              key={topic.tag}
              href={`/tag/${encodeURIComponent(topic.tag)}`}
              className="group flex items-start space-x-3 rounded-xl p-2 -mx-1 hover:bg-muted/50 transition focus:outline-none focus:ring-1 focus:ring-primary"
            >
              <span className="text-xs font-bold text-muted-foreground/70 w-5 pt-0.5 group-hover:text-primary transition">
                #{topic.rank}
              </span>
              <div className="flex-1 min-w-0">
                <p className="text-xs sm:text-sm font-semibold text-foreground truncate group-hover:text-primary transition">
                  #{topic.tag}
                </p>
                <p className="text-[11px] text-muted-foreground">
                  {topic.postCount.toLocaleString("id-ID")} celotehan
                </p>
              </div>
            </Link>
          ))}
        </nav>
      )}
    </aside>
  );
}
