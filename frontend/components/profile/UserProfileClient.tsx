"use client";

import React, { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Calendar, CheckCircle2, Repeat2, UserX, Edit3, Camera } from "lucide-react";
import { useProfile } from "../../hooks/useProfile";
import { useAuth } from "../../hooks/useAuth";
import { Button } from "../ui/button";
import { Skeleton } from "../ui/skeleton";
import { EditProfileModal } from "./EditProfileModal";
import { FollowButton } from "../social/FollowButton";
import { FollowStatusBadge } from "../social/FollowStatusBadge";
import { UserActionMenu } from "../social/UserActionMenu";
import { FollowListModal } from "../social/FollowListModal";
import { BlockedProfileFallback } from "../social/BlockedProfileFallback";
import { UserReplyCard } from "../posts/UserReplyCard";
import { InfiniteFeed } from "../posts/InfiniteFeed";
import { postsApi } from "../../lib/api/posts";
import { timelinesApi } from "../../lib/api/timelines";
import { engagementsApi } from "../../lib/api/engagements";
import { resolveBannerStyle } from "../../lib/utils/gradients";
import type { BookmarkedPost } from "../../types/api";

interface UserProfileClientProps {
  username: string;
}

type TabType = "posts" | "replies" | "media" | "likes" | "bookmarks";

export function UserProfileClient({ username }: UserProfileClientProps) {
  const { profile, isLoading, isError } = useProfile(username);
  const { user: currentUser } = useAuth();
  const [activeTab, setActiveTab] = useState<TabType>("posts");
  const [isEditModalOpen, setIsEditModalOpen] = useState(false);
  const [followListModalState, setFollowListModalState] = useState<{
    isOpen: boolean;
    tab: "followers" | "following";
  }>({
    isOpen: false,
    tab: "followers",
  });

  const queryClient = useQueryClient();

  // Fetch User Replies for Replies tab
  const {
    data: userRepliesData,
    isLoading: isRepliesLoading,
    refetch: refetchUserReplies,
  } = useQuery({
    queryKey: ["user-replies", username],
    queryFn: async () => {
      const res = await postsApi.getUserReplies(username, null, 30);
      return res.data;
    },
    enabled: !!profile && activeTab === "replies",
  });

  const isOwnProfile = !!(
    currentUser &&
    profile &&
    currentUser.username.toLowerCase() === profile.username.toLowerCase()
  );

  if (isLoading) {
    return (
      <main className="max-w-4xl mx-auto px-4 py-8 space-y-6">
        <div className="rounded-2xl border border-border bg-card p-6 sm:p-8 space-y-6">
          <div className="flex items-start justify-between">
            <Skeleton className="h-24 w-24 rounded-full" />
            <Skeleton className="h-9 w-28 rounded-lg" />
          </div>
          <div className="space-y-2">
            <Skeleton className="h-6 w-48" />
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-16 w-full mt-4" />
          </div>
          <div className="flex space-x-6 pt-4 border-t border-border">
            <Skeleton className="h-4 w-20" />
            <Skeleton className="h-4 w-20" />
            <Skeleton className="h-4 w-20" />
          </div>
        </div>
      </main>
    );
  }

  // Handle blocked profile states
  if (profile?.relationship?.isBlocked) {
    return (
      <BlockedProfileFallback
        targetUserId={profile.id}
        targetUsername={profile.username}
        isBlockedByMe={true}
      />
    );
  }

  if (profile?.relationship?.isBlockedBy) {
    return (
      <BlockedProfileFallback
        targetUsername={username}
        isBlockedByMe={false}
      />
    );
  }

  if (isError || !profile) {
    return (
      <main className="max-w-4xl mx-auto px-4 py-16 text-center space-y-4">
        <div className="inline-flex h-16 w-16 items-center justify-center rounded-2xl bg-destructive/10 text-destructive mb-2">
          <UserX className="h-8 w-8" />
        </div>
        <h1 className="text-2xl font-bold text-foreground">Pengguna Tidak Ditemukan</h1>
        <p className="text-sm text-muted-foreground max-w-md mx-auto">
          Akun @{username} tidak ditemukan atau mungkin telah dinonaktifkan.
        </p>
      </main>
    );
  }

  const formattedDate = new Date(profile.createdAt).toLocaleDateString("id-ID", {
    month: "long",
    year: "numeric",
  });

  return (
    <main className="max-w-4xl mx-auto px-4 py-8 space-y-6">
      {/* Profile Header Card */}
      <section className="rounded-2xl border border-border bg-card shadow-sm overflow-hidden">
        {/* Background Banner with Deterministic Gradient Fallback */}
        <div
          className="w-full h-36 sm:h-48 relative overflow-hidden transition-all group bg-muted/30"
          style={resolveBannerStyle(profile.bannerUrl, profile.username)}
        >
          {isOwnProfile && (
            <button
              type="button"
              onClick={() => setIsEditModalOpen(true)}
              className="absolute top-3 right-3 inline-flex items-center space-x-1 bg-black/50 hover:bg-black/70 text-white text-xs font-medium px-2.5 py-1 rounded-md backdrop-blur-sm shadow-sm transition"
              aria-label="Ubah foto header profil"
            >
              <Camera className="h-3.5 w-3.5 mr-1" />
              <span>Ubah Header</span>
            </button>
          )}
        </div>

        {/* Profile Details Container */}
        <div className="px-6 pb-6 sm:px-8 sm:pb-8 pt-0 space-y-4">
          {/* Top row with Overlapping Avatar and Action Buttons */}
          <div className="flex items-end justify-between -mt-14 sm:-mt-16">
            <div className="relative">
              <div className="flex h-24 w-24 sm:h-28 sm:w-28 items-center justify-center rounded-full border-4 border-card bg-primary text-3xl font-extrabold text-primary-foreground shadow-md overflow-hidden flex-shrink-0">
                {profile.avatarUrl ? (
                  <img
                    src={profile.avatarUrl}
                    alt={profile.displayName || profile.username}
                    loading="eager"
                    decoding="async"
                    className="h-full w-full object-cover"
                  />
                ) : (
                  profile.displayName?.charAt(0).toUpperCase() ||
                  profile.username?.charAt(0).toUpperCase()
                )}
              </div>
            </div>

            <div className="flex items-center space-x-2 pb-1">
              {isOwnProfile ? (
                <Button
                  variant="outline"
                  size="sm"
                  className="font-medium"
                  onClick={() => setIsEditModalOpen(true)}
                >
                  <Edit3 className="h-3.5 w-3.5 mr-1.5" />
                  Edit Profil
                </Button>
              ) : (
                <>
                  <FollowButton
                    targetUserId={profile.id}
                    targetUsername={profile.username}
                    isFollowing={profile.relationship?.isFollowing ?? false}
                    isBlocked={profile.relationship?.isBlocked ?? false}
                    size="sm"
                  />
                  <UserActionMenu
                    targetUserId={profile.id}
                    targetUsername={profile.username}
                    isBlocked={profile.relationship?.isBlocked ?? false}
                  />
                </>
              )}
            </div>
          </div>

          {/* User Identity Details */}
          <div className="space-y-2">
            <div>
              <div className="flex items-center space-x-1.5 leading-tight">
                <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-foreground leading-tight">
                  {profile.displayName}
                </h1>
                {profile.isVerified && (
                  <CheckCircle2 className="h-4 w-4 sm:h-5 sm:w-5 text-blue-500 fill-blue-500/10 flex-shrink-0" aria-label="Akun Terverifikasi" />
                )}
                <FollowStatusBadge
                  isFollowing={profile.relationship?.isFollowing}
                  isFollowedBy={profile.relationship?.isFollowedBy}
                  isBlocked={profile.relationship?.isBlocked}
                />
              </div>
              <p className="text-xs sm:text-sm text-muted-foreground font-normal leading-tight mt-0.5">
                @{profile.username}
              </p>
            </div>

            {profile.bio && (
              <p className="text-sm text-foreground/90 whitespace-pre-wrap leading-relaxed pt-0.5">
                {profile.bio}
              </p>
            )}

            <div className="flex items-center text-xs text-muted-foreground space-x-1 pt-0.5">
              <Calendar className="h-3.5 w-3.5" />
              <span>Bergabung {formattedDate}</span>
            </div>
          </div>

          {/* Statistics Bar */}
          <div className="flex items-center space-x-6 pt-3 border-t border-border text-sm">
            <div className="space-x-1">
              <span className="font-bold text-foreground">{profile.stats.postCount}</span>
              <span className="text-muted-foreground text-xs">Postingan</span>
            </div>
            <button
              type="button"
              onClick={() => setFollowListModalState({ isOpen: true, tab: "following" })}
              className="space-x-1 hover:underline text-left cursor-pointer focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring rounded"
            >
              <span className="font-bold text-foreground">{profile.stats.followingCount}</span>
              <span className="text-muted-foreground text-xs">Mengikuti</span>
            </button>
            <button
              type="button"
              onClick={() => setFollowListModalState({ isOpen: true, tab: "followers" })}
              className="space-x-1 hover:underline text-left cursor-pointer focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring rounded"
            >
              <span className="font-bold text-foreground">{profile.stats.followerCount}</span>
              <span className="text-muted-foreground text-xs">Pengikut</span>
            </button>
          </div>
        </div>
      </section>

      {/* Profile Navigation Tabs */}
      <section className="rounded-2xl border border-border bg-card overflow-hidden shadow-sm">
        <div className="flex border-b border-border text-sm font-medium" role="tablist">
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === "posts"}
            onClick={() => setActiveTab("posts")}
            className={`flex-1 py-3 text-center transition-colors border-b-2 ${
              activeTab === "posts"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Postingan
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === "replies"}
            onClick={() => setActiveTab("replies")}
            className={`flex-1 py-3 text-center transition-colors border-b-2 ${
              activeTab === "replies"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Balasan
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === "media"}
            onClick={() => setActiveTab("media")}
            className={`flex-1 py-3 text-center transition-colors border-b-2 ${
              activeTab === "media"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Media
          </button>
          <button
            type="button"
            role="tab"
            aria-selected={activeTab === "likes"}
            onClick={() => setActiveTab("likes")}
            className={`flex-1 py-3 text-center transition-colors border-b-2 ${
              activeTab === "likes"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Disukai
          </button>
          {isOwnProfile && (
            <button
              type="button"
              role="tab"
              aria-selected={activeTab === "bookmarks"}
              onClick={() => setActiveTab("bookmarks")}
              className={`flex-1 py-3 text-center transition-colors border-b-2 ${
                activeTab === "bookmarks"
                  ? "border-primary text-primary font-semibold"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              }`}
            >
              Markah
            </button>
          )}
        </div>

        {/* Tab Content */}
        <div className="p-4 sm:p-6" role="tabpanel">
          {activeTab === "posts" && (
            <InfiniteFeed
              queryKey={["user-posts", profile.id]}
              queryFn={async (cursor) => {
                const res = await timelinesApi.getUserPosts(profile.id, "posts", cursor, 20);
                return res.data;
              }}
              emptyTitle="Belum ada postingan"
              emptyDescription={`Saat @${profile.username} membagikan postingan, mereka akan muncul di sini.`}
            />
          )}

          {activeTab === "media" && (
            <InfiniteFeed
              queryKey={["user-media", profile.id]}
              queryFn={async (cursor) => {
                const res = await timelinesApi.getUserPosts(profile.id, "media", cursor, 20);
                return res.data;
              }}
              emptyTitle="Belum ada media"
              emptyDescription={`Foto dan media yang dibagikan oleh @${profile.username} akan muncul di sini.`}
            />
          )}

          {activeTab === "replies" && (
            <div>
              {isRepliesLoading ? (
                <div className="space-y-4">
                  <div className="rounded-2xl border border-border bg-card p-6 space-y-3 animate-pulse">
                    <div className="flex items-center space-x-3">
                      <div className="h-10 w-10 rounded-full bg-muted" />
                      <div className="space-y-1.5 flex-1">
                        <div className="h-4 w-32 rounded bg-muted" />
                        <div className="h-3 w-20 rounded bg-muted" />
                      </div>
                    </div>
                    <div className="h-12 w-full rounded bg-muted" />
                  </div>
                </div>
              ) : userRepliesData && userRepliesData.items.length > 0 ? (
                <div className="space-y-4">
                  {userRepliesData.items.map((reply) => (
                    <UserReplyCard
                      key={reply.id}
                      reply={reply}
                      onDeleted={() => {
                        queryClient.invalidateQueries({ queryKey: ["user-replies", username] });
                        refetchUserReplies();
                      }}
                    />
                  ))}
                </div>
              ) : (
                <div className="p-12 text-center space-y-3">
                  <div className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-muted text-muted-foreground">
                    <Repeat2 className="h-6 w-6" />
                  </div>
                  <h3 className="font-semibold text-foreground">Belum ada balasan</h3>
                  <p className="text-xs text-muted-foreground max-w-sm mx-auto">
                    Percakapan dan balasan oleh @{profile.username} akan terlihat di sini.
                  </p>
                </div>
              )}
            </div>
          )}

          {activeTab === "likes" && (
            <InfiniteFeed
              queryKey={["user-likes", profile.id]}
              queryFn={async (cursor) => {
                const res = await timelinesApi.getUserLikes(profile.id, cursor, 20);
                return res.data;
              }}
              emptyTitle="Belum ada postingan yang disukai"
              emptyDescription={
                isOwnProfile
                  ? "Anda belum menyukai postingan apa pun."
                  : `Postingan yang disukai oleh @${profile.username} akan muncul di sini.`
              }
            />
          )}

          {activeTab === "bookmarks" && (
            <InfiniteFeed
              queryKey={["user-bookmarks", profile.id]}
              queryFn={async (cursor) => {
                const res = await engagementsApi.getBookmarks(cursor, 20);
                const normalizedItems = (res.data.items || []).map((item: BookmarkedPost) => ({
                  ...item,
                  id: item.id || item.postId || item.bookmarkId,
                }));
                return {
                  ...res.data,
                  items: normalizedItems,
                };
              }}
              emptyTitle="Belum ada markah tersimpan"
              emptyDescription="Simpan postingan untuk menemukannya di sini nanti."
            />
          )}
        </div>
      </section>

      {/* Edit Profile Modal Dialog */}
      {currentUser && (
        <EditProfileModal
          isOpen={isEditModalOpen}
          onClose={() => setIsEditModalOpen(false)}
          currentUser={currentUser}
        />
      )}

      {/* Followers / Following List Modal Dialog */}
      <FollowListModal
        key={`${profile.id}-${followListModalState.tab}`}
        isOpen={followListModalState.isOpen}
        onClose={() => setFollowListModalState((prev) => ({ ...prev, isOpen: false }))}
        targetUserId={profile.id}
        targetUsername={profile.username}
        targetDisplayName={profile.displayName}
        initialTab={followListModalState.tab}
      />
    </main>
  );
}
