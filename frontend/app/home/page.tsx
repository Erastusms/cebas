"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { Sparkles, Search, Compass, RefreshCw } from "lucide-react";
import { useAuth } from "../../hooks/useAuth";
import { Button } from "../../components/ui/button";
import { Input } from "../../components/ui/input";
import { PostComposer } from "../../components/posts/PostComposer";
import { InfiniteFeed } from "../../components/posts/InfiniteFeed";
import { NewPostsBanner } from "../../components/posts/NewPostsBanner";
import { useRealtimeEvent } from "../../hooks/useRealtime";
import { timelinesApi } from "../../lib/api/timelines";

export default function HomeFeedPage() {
  const { user, isAuthenticated } = useAuth();
  const queryClient = useQueryClient();
  const router = useRouter();
  const [searchHandle, setSearchHandle] = useState("");
  const [newPostsCount, setNewPostsCount] = useState(0);

  // Real-time listener for incoming posts from followed users
  useRealtimeEvent("NewPostAvailable", (data) => {
    if (data.authorId !== user?.id) {
      setNewPostsCount((prev) => prev + 1);
    }
  });

  const handleBannerClick = () => {
    if (typeof window !== "undefined") {
      window.scrollTo({ top: 0, behavior: "smooth" });
    }
    queryClient.invalidateQueries({ queryKey: ["home-timeline", user?.id] });
    setNewPostsCount(0);
  };

  const handleSearchProfile = (e: React.FormEvent) => {
    e.preventDefault();
    if (!searchHandle.trim()) return;
    const cleanHandle = searchHandle.trim().replace(/^@/, "");
    router.push(`/user/${encodeURIComponent(cleanHandle)}`);
  };

  const handlePostCreated = () => {
    queryClient.invalidateQueries({ queryKey: ["home-timeline", user?.id] });
  };

  return (
    <main className="min-h-[calc(100vh-4rem)] bg-background text-foreground">
      <div className="mx-auto max-w-2xl space-y-6 px-4 py-6 sm:px-6 relative">
        {/* Floating Pill Banner for New Incoming Posts */}
        <NewPostsBanner count={newPostsCount} onClick={handleBannerClick} />

        {/* Post Creation Box for Authenticated Users */}
        {isAuthenticated && (
          <section aria-labelledby="create-post-title" className="space-y-3">
            <div className="flex items-center space-x-2">
              <Sparkles className="h-5 w-5 text-primary" />
              <h2 id="create-post-title" className="text-lg font-bold text-foreground">
                Bagikan Pikiran Anda
              </h2>
            </div>
            <PostComposer onPostCreated={handlePostCreated} />
          </section>
        )}

        {/* Home Feed Stream */}
        <section aria-labelledby="home-timeline-title" className="space-y-4">
          <div className="flex items-center justify-between border-b border-border pb-2">
            <h1 id="home-timeline-title" className="text-xl font-extrabold tracking-tight text-foreground">
              Linimasa
            </h1>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => queryClient.invalidateQueries({ queryKey: ["home-timeline", user?.id] })}
              className="text-xs text-muted-foreground hover:text-foreground h-8 px-2"
              aria-label="Refresh timeline feed"
            >
              <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
              <span>Segarkan</span>
            </Button>
          </div>

          <InfiniteFeed
            queryKey={["home-timeline", user?.id]}
            queryFn={async (cursor) => {
              const res = await timelinesApi.getHomeTimeline(cursor, 20);
              return res.data;
            }}
            emptyTitle="Belum ada postingan, ikuti akun lain untuk mulai melihat linimasa."
            emptyDescription="Ikuti pembuat konten favorit Anda atau buat postingan pertama Anda untuk mulai berinteraksi di CEBAS."
            emptyAction={
              <div className="flex flex-col sm:flex-row items-center justify-center gap-2 pt-2">
                <Link href="/">
                  <Button variant="default" size="sm" className="text-xs">
                    <Compass className="mr-1.5 h-3.5 w-3.5" />
                    Jelajahi Profil
                  </Button>
                </Link>
              </div>
            }
          />
        </section>

        {/* Quick Profile Lookup Footer */}
        <section className="rounded-2xl border border-border bg-card p-6 shadow-sm space-y-3">
          <div className="flex items-center space-x-2">
            <Search className="h-4 w-4 text-primary" />
            <h3 className="text-sm font-semibold text-foreground">
              Cari Akun Pengguna
            </h3>
          </div>
          <form onSubmit={handleSearchProfile} className="flex gap-2">
            <Input
              placeholder="Ketik username, misal: johndoe"
              value={searchHandle}
              onChange={(e) => setSearchHandle(e.target.value)}
              className="text-sm"
              aria-label="Cari profil pengguna"
            />
            <Button type="submit" variant="default" size="md">
              Cari
            </Button>
          </form>
        </section>
      </div>
    </main>
  );
}
