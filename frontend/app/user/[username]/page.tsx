"use client";

import React, { useState, use } from "react";
import { Calendar, CheckCircle2, MessageSquare, Repeat2, Image as ImageIcon, Heart, UserX, Edit3, Camera } from "lucide-react";
import { useProfile } from "../../../hooks/useProfile";
import { useAuth } from "../../../hooks/useAuth";
import { Button } from "../../../components/ui/button";
import { Skeleton } from "../../../components/ui/skeleton";
import { EditProfileModal } from "../../../components/profile/EditProfileModal";
import { FollowButton } from "../../../components/social/FollowButton";
import { FollowStatusBadge } from "../../../components/social/FollowStatusBadge";
import { UserActionMenu } from "../../../components/social/UserActionMenu";
import { FollowListModal } from "../../../components/social/FollowListModal";
import { BlockedProfileFallback } from "../../../components/social/BlockedProfileFallback";
import { resolveBannerStyle } from "../../../lib/utils/gradients";

interface ProfilePageProps {
  params: Promise<{
    username: string;
  }>;
}

type TabType = "posts" | "replies" | "media" | "likes";

export default function UserProfilePage({ params }: ProfilePageProps) {
  const resolvedParams = use(params);
  const rawUsername = resolvedParams.username;
  const username = decodeURIComponent(rawUsername);

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
        <h1 className="text-2xl font-bold text-foreground">User Not Found</h1>
        <p className="text-sm text-muted-foreground max-w-md mx-auto">
          The account @{username} doesn&apos;t exist or may have been deleted.
        </p>
      </main>
    );
  }

  const formattedDate = new Date(profile.createdAt).toLocaleDateString("en-US", {
    month: "long",
    year: "numeric",
  });

  return (
    <main className="max-w-4xl mx-auto px-4 py-8 space-y-6">
      {/* Profile Header Card */}
      <section className="rounded-2xl border border-border bg-card shadow-sm overflow-hidden">
        {/* Background Banner with Deterministic Gradient Fallback */}
        <div
          className="w-full h-36 sm:h-48 relative overflow-hidden transition-all group"
          style={resolveBannerStyle(profile.bannerUrl, profile.username)}
        >
          {isOwnProfile && (
            <button
              type="button"
              onClick={() => setIsEditModalOpen(true)}
              className="absolute top-3 right-3 inline-flex items-center space-x-1 bg-black/50 hover:bg-black/70 text-white text-xs font-medium px-2.5 py-1 rounded-md backdrop-blur-sm shadow-sm transition"
            >
              <Camera className="h-3.5 w-3.5 mr-1" />
              <span>Edit Header</span>
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
                  Edit Profile
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

          {/* User Identity Details with Tight Twitter-Style Typography */}
          <div className="space-y-2">
            <div>
              <div className="flex items-center space-x-1.5 leading-tight">
                <h1 className="text-xl sm:text-2xl font-bold tracking-tight text-foreground leading-tight">
                  {profile.displayName}
                </h1>
                {profile.isVerified && (
                  <CheckCircle2 className="h-4 w-4 sm:h-5 sm:w-5 text-blue-500 fill-blue-500/10 flex-shrink-0" aria-label="Verified Account" />
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
              <span>Joined {formattedDate}</span>
            </div>
          </div>

          {/* Statistics Bar */}
          <div className="flex items-center space-x-6 pt-3 border-t border-border text-sm">
            <div className="space-x-1">
              <span className="font-bold text-foreground">{profile.stats.postCount}</span>
              <span className="text-muted-foreground text-xs">Posts</span>
            </div>
            <button
              type="button"
              onClick={() => setFollowListModalState({ isOpen: true, tab: "following" })}
              className="space-x-1 hover:underline text-left cursor-pointer focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring rounded"
            >
              <span className="font-bold text-foreground">{profile.stats.followingCount}</span>
              <span className="text-muted-foreground text-xs">Following</span>
            </button>
            <button
              type="button"
              onClick={() => setFollowListModalState({ isOpen: true, tab: "followers" })}
              className="space-x-1 hover:underline text-left cursor-pointer focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring rounded"
            >
              <span className="font-bold text-foreground">{profile.stats.followerCount}</span>
              <span className="text-muted-foreground text-xs">Followers</span>
            </button>
          </div>
        </div>
      </section>

      {/* Profile Navigation Tabs */}
      <section className="rounded-2xl border border-border bg-card overflow-hidden shadow-sm">
        <div className="flex border-b border-border text-sm font-medium">
          <button
            type="button"
            onClick={() => setActiveTab("posts")}
            className={`flex-1 py-3 text-center transition-colors border-b-2 ${
              activeTab === "posts"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Posts
          </button>
          <button
            type="button"
            onClick={() => setActiveTab("replies")}
            className={`flex-1 py-3 text-center transition-colors border-b-2 ${
              activeTab === "replies"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Replies
          </button>
          <button
            type="button"
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
            onClick={() => setActiveTab("likes")}
            className={`flex-1 py-3 text-center transition-colors border-b-2 ${
              activeTab === "likes"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Likes
          </button>
        </div>

        {/* Tab Content Placeholders */}
        <div className="p-12 text-center space-y-3">
          {activeTab === "posts" && (
            <>
              <div className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-muted text-muted-foreground">
                <MessageSquare className="h-6 w-6" />
              </div>
              <h3 className="font-semibold text-foreground">No posts yet</h3>
              <p className="text-xs text-muted-foreground max-w-sm mx-auto">
                When @{profile.username} publishes posts, they will appear here.
              </p>
            </>
          )}

          {activeTab === "replies" && (
            <>
              <div className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-muted text-muted-foreground">
                <Repeat2 className="h-6 w-6" />
              </div>
              <h3 className="font-semibold text-foreground">No replies yet</h3>
              <p className="text-xs text-muted-foreground max-w-sm mx-auto">
                Conversations and replies by @{profile.username} will be visible here.
              </p>
            </>
          )}

          {activeTab === "media" && (
            <>
              <div className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-muted text-muted-foreground">
                <ImageIcon className="h-6 w-6" />
              </div>
              <h3 className="font-semibold text-foreground">No media shared</h3>
              <p className="text-xs text-muted-foreground max-w-sm mx-auto">
                Photos, videos, and media attached to posts will appear here.
              </p>
            </>
          )}

          {activeTab === "likes" && (
            <>
              <div className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-muted text-muted-foreground">
                <Heart className="h-6 w-6" />
              </div>
              <h3 className="font-semibold text-foreground">No liked posts</h3>
              <p className="text-xs text-muted-foreground max-w-sm mx-auto">
                Posts liked by @{profile.username} will show up here.
              </p>
            </>
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
        isOpen={followListModalState.isOpen}
        onClose={() => setFollowListModalState({ isOpen: false, tab: "followers" })}
        targetUserId={profile.id}
        targetUsername={profile.username}
        targetDisplayName={profile.displayName}
        initialTab={followListModalState.tab}
      />
    </main>
  );
}