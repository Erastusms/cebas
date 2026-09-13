"use client";

import React, { useState, useEffect, useRef, useMemo } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { Search, User, RefreshCw, CheckCircle2, MessageSquare, Heart, Bookmark, AlertCircle, ArrowRight } from "lucide-react";
import { useSearchPosts, useSearchUsers, useSearchSummary } from "../../hooks/useSearch";
import { SearchBar } from "./SearchBar";
import { SearchHighlight } from "./SearchHighlight";
import { Button } from "../ui/button";
import { FollowButton } from "../social/FollowButton";
import { useAuth } from "../../hooks/useAuth";
import type { SearchPostItem, SearchUserItem } from "../../lib/api/search";

export type SearchTab = "semua" | "celotehan" | "akun";

export function SearchResultsClient() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const initialQuery = searchParams.get("q") ?? "";
  const initialTab = (searchParams.get("tab") as SearchTab) ?? "semua";

  const [query, setQuery] = useState(initialQuery);
  const [activeTab, setActiveTab] = useState<SearchTab>(initialTab);
  const { user: currentUser } = useAuth();

  // Sync URL search params
  useEffect(() => {
    const q = searchParams.get("q") ?? "";
    const tab = (searchParams.get("tab") as SearchTab) ?? "semua";
    setQuery(q);
    if (["semua", "celotehan", "akun"].includes(tab)) {
      setActiveTab(tab);
    }
  }, [searchParams]);

  const handleTabChange = (tab: SearchTab) => {
    setActiveTab(tab);
    const params = new URLSearchParams(searchParams.toString());
    params.set("tab", tab);
    router.push(`/search?${params.toString()}`);
  };

  const handleSearchSubmitted = (newQuery: string) => {
    setQuery(newQuery);
    const params = new URLSearchParams();
    params.set("q", newQuery);
    params.set("tab", activeTab);
    router.push(`/search?${params.toString()}`);
  };

  return (
    <div className="mx-auto max-w-3xl px-4 py-6 sm:px-6 space-y-6">
      {/* Top Search Input Bar */}
      <div className="flex items-center space-x-3">
        <SearchBar
          initialQuery={query}
          onSearchSubmitted={handleSearchSubmitted}
          className="max-w-none flex-1"
        />
      </div>

      {/* Tabs Bar */}
      <div className="flex border-b border-border">
        <button
          type="button"
          onClick={() => handleTabChange("semua")}
          className={`flex-1 py-3 text-center text-xs sm:text-sm font-semibold transition border-b-2 -mb-px ${
            activeTab === "semua"
              ? "border-primary text-primary"
              : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
        >
          Semua
        </button>
        <button
          type="button"
          onClick={() => handleTabChange("celotehan")}
          className={`flex-1 py-3 text-center text-xs sm:text-sm font-semibold transition border-b-2 -mb-px ${
            activeTab === "celotehan"
              ? "border-primary text-primary"
              : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
        >
          Celotehan
        </button>
        <button
          type="button"
          onClick={() => handleTabChange("akun")}
          className={`flex-1 py-3 text-center text-xs sm:text-sm font-semibold transition border-b-2 -mb-px ${
            activeTab === "akun"
              ? "border-primary text-primary"
              : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
        >
          Akun
        </button>
      </div>

      {/* Query Content Container */}
      {!query.trim() ? (
        <div className="rounded-2xl border border-border bg-card p-12 text-center space-y-3">
          <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-primary/10 text-primary">
            <Search className="h-6 w-6" />
          </div>
          <p className="text-base font-bold text-foreground">
            Mulai Pencarian di CEBAS
          </p>
          <p className="text-xs text-muted-foreground max-w-md mx-auto">
            Ketik kata kunci untuk menemukan celotehan, akun pengguna, atau topik yang sedang hangat diperbincangkan.
          </p>
        </div>
      ) : (
        <>
          {activeTab === "semua" && (
            <TabSemua
              query={query}
              onNavigateToTab={handleTabChange}
              currentUserId={currentUser?.id}
            />
          )}
          {activeTab === "celotehan" && <TabCelotehan query={query} />}
          {activeTab === "akun" && <TabAkun query={query} currentUserId={currentUser?.id} />}
        </>
      )}
    </div>
  );
}

// --------------------------------------------------------------------------------
// TAB: SEMUA (COMBINED)
// --------------------------------------------------------------------------------
function TabSemua({
  query,
  onNavigateToTab,
  currentUserId,
}: {
  query: string;
  onNavigateToTab: (tab: SearchTab) => void;
  currentUserId?: string;
}) {
  const { data, isLoading, isError, error, refetch } = useSearchSummary(query);

  if (isLoading) {
    return <SearchSkeleton />;
  }

  if (isError) {
    return <SearchErrorView error={error as Error} onRetry={() => refetch()} />;
  }

  const posts = data?.posts ?? [];
  const users = data?.users ?? [];

  if (posts.length === 0 && users.length === 0) {
    return <SearchEmptyView query={query} />;
  }

  return (
    <div className="space-y-6">
      {/* Accounts Preview Section */}
      {users.length > 0 && (
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-bold text-foreground uppercase tracking-wider">
              Akun
            </h2>
            <button
              type="button"
              onClick={() => onNavigateToTab("akun")}
              className="text-xs font-semibold text-primary hover:underline flex items-center space-x-1"
            >
              <span>Lihat Semua Akun</span>
              <ArrowRight className="h-3 w-3" />
            </button>
          </div>
          <div className="divide-y divide-border rounded-2xl border border-border bg-card overflow-hidden">
            {users.map((u) => (
              <UserResultRow key={u.id} user={u} currentUserId={currentUserId} />
            ))}
          </div>
        </section>
      )}

      {/* Posts Section */}
      {posts.length > 0 && (
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-sm font-bold text-foreground uppercase tracking-wider">
              Celotehan
            </h2>
            <button
              type="button"
              onClick={() => onNavigateToTab("celotehan")}
              className="text-xs font-semibold text-primary hover:underline flex items-center space-x-1"
            >
              <span>Lihat Semua Celotehan</span>
              <ArrowRight className="h-3 w-3" />
            </button>
          </div>
          <div className="space-y-3">
            {posts.map((post) => (
              <PostResultCard key={post.id} post={post} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}

// --------------------------------------------------------------------------------
// TAB: CELOTEHAN (POSTS WITH INFINITE SCROLL)
// --------------------------------------------------------------------------------
function TabCelotehan({ query }: { query: string }) {
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
  } = useSearchPosts(query);

  const posts = useMemo(() => {
    if (!data?.pages) return [];
    const seen = new Set<string>();
    const list: SearchPostItem[] = [];
    for (const page of data.pages) {
      if (!page?.items) continue;
      for (const item of page.items) {
        if (!seen.has(item.id)) {
          seen.add(item.id);
          list.push(item);
        }
      }
    }
    return list;
  }, [data]);

  const sentinelRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const el = sentinelRef.current;
    if (!el) return;

    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0].isIntersecting && hasNextPage && !isFetchingNextPage) {
          fetchNextPage();
        }
      },
      { rootMargin: "250px" }
    );

    observer.observe(el);
    return () => observer.disconnect();
  }, [hasNextPage, isFetchingNextPage, fetchNextPage]);

  const handleRetryNextPage = () => {
    void fetchNextPage();
  };

  if (isLoading) {
    return <SearchSkeleton />;
  }

  if (isError) {
    return <SearchErrorView error={error as Error} onRetry={() => refetch()} />;
  }

  if (posts.length === 0) {
    return <SearchEmptyView query={query} />;
  }

  return (
    <div className="space-y-4">
      {posts.map((post) => (
        <PostResultCard key={post.id} post={post} />
      ))}

      {isFetchNextPageError && (
        <div className="rounded-2xl border border-destructive/20 bg-destructive/5 p-4 text-center space-y-2">
          <p className="text-xs text-destructive">Gagal memuat celotehan berikutnya</p>
          <Button size="sm" variant="outline" onClick={handleRetryNextPage}>
            <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
            Coba Lagi
          </Button>
        </div>
      )}

      {isFetchingNextPage && (
        <div className="flex justify-center py-4 text-xs text-muted-foreground space-x-2">
          <RefreshCw className="h-4 w-4 animate-spin text-primary" />
          <span>Memuat lebih banyak celotehan...</span>
        </div>
      )}

      <div ref={sentinelRef} className="h-1 w-full" aria-hidden="true" />
    </div>
  );
}

// --------------------------------------------------------------------------------
// TAB: AKUN (USERS)
// --------------------------------------------------------------------------------
function TabAkun({ query, currentUserId }: { query: string; currentUserId?: string }) {
  const { data: users, isLoading, isError, error, refetch } = useSearchUsers(query);

  if (isLoading) {
    return <SearchSkeleton />;
  }

  if (isError) {
    return <SearchErrorView error={error as Error} onRetry={() => refetch()} />;
  }

  if (!users || users.length === 0) {
    return <SearchEmptyView query={query} />;
  }

  return (
    <div className="divide-y divide-border rounded-2xl border border-border bg-card overflow-hidden">
      {users.map((u) => (
        <UserResultRow key={u.id} user={u} showBio currentUserId={currentUserId} />
      ))}
    </div>
  );
}

// --------------------------------------------------------------------------------
// CARD COMPONENTS
// --------------------------------------------------------------------------------
function PostResultCard({ post }: { post: SearchPostItem }) {
  return (
    <article className="rounded-2xl border border-border bg-card p-5 space-y-3 transition hover:border-border/80 shadow-sm">
      {/* Author Header */}
      <div className="flex items-center space-x-3">
        <Link
          href={`/user/${encodeURIComponent(post.author.username)}`}
          className="flex h-10 w-10 items-center justify-center rounded-full bg-primary/10 overflow-hidden flex-shrink-0 font-bold text-primary hover:opacity-90"
        >
          {post.author.avatarUrl ? (
            <img
              src={post.author.avatarUrl}
              alt={post.author.displayName || post.author.username}
              className="h-full w-full object-cover"
            />
          ) : (
            post.author.displayName?.charAt(0).toUpperCase() || <User className="h-5 w-5" />
          )}
        </Link>
        <div className="min-w-0 flex-1">
          <div className="flex items-center space-x-1.5">
            <Link
              href={`/user/${encodeURIComponent(post.author.username)}`}
              className="font-bold text-sm text-foreground hover:underline truncate"
            >
              {post.author.displayName || post.author.username}
            </Link>
            {post.author.isVerified && (
              <CheckCircle2 className="h-3.5 w-3.5 text-primary flex-shrink-0" />
            )}
            <span className="text-xs text-muted-foreground truncate">
              @{post.author.username}
            </span>
          </div>
          <span className="text-[11px] text-muted-foreground">
            {new Date(post.createdAt).toLocaleDateString("id-ID", {
              day: "numeric",
              month: "short",
              year: "numeric",
            })}
          </span>
        </div>
      </div>

      {/* Post Content with Safe Highlight Rendering */}
      <Link href={`/post/${post.id}`} className="block">
        <p className="text-sm leading-relaxed text-foreground break-words">
          <SearchHighlight
            text={post.highlightedContent}
            fallbackText={post.content}
          />
        </p>
      </Link>

      {/* Post Actions Footer */}
      <div className="flex items-center space-x-6 pt-1 text-xs text-muted-foreground">
        <div className="flex items-center space-x-1.5">
          <MessageSquare className="h-3.5 w-3.5" />
          <span>{post.replyCount}</span>
        </div>
        <div className="flex items-center space-x-1.5">
          <Heart className={`h-3.5 w-3.5 ${post.liked ? "fill-destructive text-destructive" : ""}`} />
          <span>{post.likeCount}</span>
        </div>
        <div className="flex items-center space-x-1.5">
          <Bookmark className={`h-3.5 w-3.5 ${post.bookmarked ? "fill-primary text-primary" : ""}`} />
          <span>{post.bookmarkCount}</span>
        </div>
      </div>
    </article>
  );
}

function UserResultRow({
  user,
  showBio = false,
  currentUserId,
}: {
  user: SearchUserItem;
  showBio?: boolean;
  currentUserId?: string;
}) {
  const { user: currentUser } = useAuth();
  const effectiveUserId = currentUserId ?? currentUser?.id;
  const isSelf = effectiveUserId === user.id;

  return (
    <div className="flex items-start justify-between p-4 space-x-3 hover:bg-muted/30 transition">
      <Link
        href={`/user/${encodeURIComponent(user.username)}`}
        className="flex items-start space-x-3 min-w-0 flex-1"
      >
        <div className="flex h-11 w-11 items-center justify-center rounded-full bg-primary/10 overflow-hidden flex-shrink-0 font-bold text-primary mt-0.5">
          {user.avatarUrl ? (
            <img
              src={user.avatarUrl}
              alt={user.displayName || user.username}
              className="h-full w-full object-cover"
            />
          ) : (
            user.displayName?.charAt(0).toUpperCase() || <User className="h-5 w-5" />
          )}
        </div>
        <div className="min-w-0 flex-1">
          <div className="flex items-center space-x-1.5">
            <span className="font-bold text-sm text-foreground hover:underline truncate">
              <SearchHighlight
                text={user.highlightedDisplayName}
                fallbackText={user.displayName || user.username}
              />
            </span>
            {user.isVerified && (
              <CheckCircle2 className="h-3.5 w-3.5 text-primary flex-shrink-0" />
            )}
          </div>
          <p className="text-xs text-muted-foreground truncate">
            @
            <SearchHighlight
              text={user.highlightedUsername}
              fallbackText={user.username}
            />
          </p>
          {showBio && (user.highlightedBio || user.bio) && (
            <p className="text-xs text-foreground/80 mt-1 line-clamp-2">
              <SearchHighlight text={user.highlightedBio} fallbackText={user.bio ?? ""} />
            </p>
          )}
        </div>
      </Link>

      {!isSelf && (
        <div className="flex-shrink-0 ml-2">
          <FollowButton
            targetUserId={user.id}
            targetUsername={user.username}
            isFollowing={user.isFollowing}
            onFollowChange={(nextFollowing) => {
              user.isFollowing = nextFollowing;
            }}
          />
        </div>
      )}
    </div>
  );
}

// --------------------------------------------------------------------------------
// FEEDBACK & STATUS COMPONENTS
// --------------------------------------------------------------------------------
function SearchEmptyView({ query }: { query: string }) {
  return (
    <div className="rounded-2xl border border-border bg-card p-12 text-center space-y-3">
      <p className="text-base font-bold text-foreground">
        Pencarian tidak ditemukan
      </p>
      <p className="text-xs text-muted-foreground max-w-sm mx-auto">
        Tidak ada hasil untuk &quot;<span className="font-semibold">{query}</span>&quot;.
        Coba gunakan kata kunci lain atau periksa ejaan kata Anda.
      </p>
    </div>
  );
}

function SearchErrorView({ error, onRetry }: { error: Error; onRetry: () => void }) {
  return (
    <div className="rounded-2xl border border-destructive/20 bg-destructive/5 p-8 text-center space-y-3">
      <div className="mx-auto flex h-10 w-10 items-center justify-center rounded-full bg-destructive/10 text-destructive">
        <AlertCircle className="h-5 w-5" />
      </div>
      <p className="text-sm font-semibold text-destructive">
        Layanan Pencarian Terganggu
      </p>
      <p className="text-xs text-muted-foreground max-w-sm mx-auto">
        {error?.message || "Layanan pencarian sedang dalam pemeliharaan atau tidak dapat dijangkau. Fitur lainnya tetap beroperasi secara normal."}
      </p>
      <Button size="sm" variant="outline" onClick={onRetry}>
        <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
        Coba Lagi
      </Button>
    </div>
  );
}

function SearchSkeleton() {
  return (
    <div className="space-y-4">
      {[1, 2, 3].map((i) => (
        <div key={i} className="rounded-2xl border border-border bg-card p-6 space-y-3 animate-pulse">
          <div className="flex items-center space-x-3">
            <div className="h-10 w-10 rounded-full bg-muted" />
            <div className="space-y-1.5 flex-1">
              <div className="h-4 w-32 rounded bg-muted" />
              <div className="h-3 w-20 rounded bg-muted" />
            </div>
          </div>
          <div className="h-10 w-full rounded bg-muted" />
        </div>
      ))}
    </div>
  );
}
