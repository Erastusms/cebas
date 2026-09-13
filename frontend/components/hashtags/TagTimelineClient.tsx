"use client";

import React from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Hash, RefreshCw, MessageSquare } from "lucide-react";
import { timelinesApi } from "../../lib/api/timelines";
import { InfiniteFeed } from "../posts/InfiniteFeed";
import { TrendingWidget } from "../trending/TrendingWidget";
import { Button } from "../ui/button";

interface TagTimelineClientProps {
  tag: string;
}

export function TagTimelineClient({ tag }: TagTimelineClientProps) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const cleanTag = tag.replace(/^#/, "").toLowerCase();

  const handleRefresh = () => {
    queryClient.invalidateQueries({ queryKey: ["tag-timeline", cleanTag] });
  };

  return (
    <main className="min-h-[calc(100vh-4rem)] bg-background text-foreground">
      <div className="mx-auto max-w-6xl px-4 py-6 sm:px-6 lg:px-8">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Main Feed Column */}
          <div className="lg:col-span-2 space-y-6">
            {/* Tag Header */}
            <header className="rounded-2xl border border-border bg-card p-4 sm:p-6 shadow-sm">
              <div className="flex items-center justify-between">
                <div className="flex items-center space-x-3">
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    onClick={() => router.back()}
                    className="h-9 w-9 p-0 rounded-full text-muted-foreground hover:text-foreground"
                    aria-label="Kembali"
                  >
                    <ArrowLeft className="h-5 w-5" />
                  </Button>

                  <div className="flex items-center space-x-2.5">
                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary text-primary-foreground font-bold shadow-sm">
                      <Hash className="h-5 w-5" />
                    </div>
                    <div>
                      <h1 className="text-xl sm:text-2xl font-extrabold tracking-tight text-foreground">
                        #{cleanTag}
                      </h1>
                      <p className="text-xs text-muted-foreground">Linimasa Topik</p>
                    </div>
                  </div>
                </div>

                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={handleRefresh}
                  className="h-8 px-2.5 text-xs text-muted-foreground hover:text-foreground"
                  aria-label="Segarkan linimasa topik"
                >
                  <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
                  <span>Segarkan</span>
                </Button>
              </div>
            </header>

            {/* Tag Posts Feed */}
            <InfiniteFeed
              queryKey={["tag-timeline", cleanTag]}
              queryFn={async (cursor) => {
                const res = await timelinesApi.getTagTimeline(cleanTag, cursor, 20);
                return res.data;
              }}
              emptyTitle={`Belum ada celotehan tentang #${cleanTag}`}
              emptyDescription={`Jadilah yang pertama membuat celotehan dengan tag #${cleanTag}!`}
              emptyAction={
                <div className="pt-2">
                  <Link href="/home">
                    <Button variant="default" size="sm" className="text-xs">
                      <MessageSquare className="mr-1.5 h-3.5 w-3.5" />
                      <span>Buat Celotehan</span>
                    </Button>
                  </Link>
                </div>
              }
            />
          </div>

          {/* Desktop Right Sidebar */}
          <div className="hidden lg:block lg:col-span-1 space-y-6">
            <TrendingWidget />
          </div>
        </div>
      </div>
    </main>
  );
}
