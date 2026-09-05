"use client";

import React, { useState, useEffect } from "react";
import Link from "next/link";
import { Users, CheckCircle2, AlertCircle, Loader2 } from "lucide-react";
import { Modal } from "../ui/modal";
import { Button } from "../ui/button";
import { Skeleton } from "../ui/skeleton";
import { FollowButton } from "./FollowButton";
import { FollowStatusBadge } from "./FollowStatusBadge";
import { useFollowers, useFollowing } from "../../hooks/useSocialGraph";
import type { SocialUser } from "../../types/api";

export interface FollowListModalProps {
  isOpen: boolean;
  onClose: () => void;
  targetUserId: string;
  targetUsername: string;
  targetDisplayName?: string;
  initialTab?: "followers" | "following";
}

export function FollowListModal({
  isOpen,
  onClose,
  targetUserId,
  targetUsername,
  targetDisplayName,
  initialTab = "followers",
}: FollowListModalProps) {
  const [tab, setTab] = useState<"followers" | "following">(initialTab);

  useEffect(() => {
    if (isOpen) {
      setTab(initialTab);
    }
  }, [isOpen, initialTab]);

  const followersQuery = useFollowers(isOpen && tab === "followers" ? targetUserId : undefined);
  const followingQuery = useFollowing(isOpen && tab === "following" ? targetUserId : undefined);

  const activeQuery = tab === "followers" ? followersQuery : followingQuery;

  // Flatten all pages of social users
  const users: SocialUser[] = activeQuery.data?.pages.flatMap((p) => p.items) ?? [];
  const isLoading = activeQuery.isLoading;
  const isFetchingNextPage = activeQuery.isFetchingNextPage;
  const hasNextPage = activeQuery.hasNextPage;
  const isError = activeQuery.isError;

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={targetDisplayName || `@${targetUsername}`}
      description={targetDisplayName ? `@${targetUsername}` : undefined}
      className="max-w-lg max-h-[85vh] flex flex-col overflow-hidden"
    >
      {/* Tab Switcher */}
      <div className="flex border-b border-border text-sm font-medium px-6 bg-card sticky top-0 z-10" role="tablist">
        <button
          type="button"
          role="tab"
          onClick={() => setTab("followers")}
          className={`flex-1 py-3 text-center transition-colors border-b-2 font-semibold cursor-pointer ${
            tab === "followers"
              ? "border-primary text-primary"
              : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
          aria-selected={tab === "followers"}
        >
          Followers
        </button>
        <button
          type="button"
          role="tab"
          onClick={() => setTab("following")}
          className={`flex-1 py-3 text-center transition-colors border-b-2 font-semibold cursor-pointer ${
            tab === "following"
              ? "border-primary text-primary"
              : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
          aria-selected={tab === "following"}
        >
          Following
        </button>
      </div>

      {/* User List Content */}
      <div className="p-6 space-y-4">
        {isLoading && (
          <div className="space-y-4">
            {[1, 2, 3, 4].map((i) => (
              <div key={i} className="flex items-center justify-between space-x-3">
                <div className="flex items-center space-x-3">
                  <Skeleton className="h-10 w-10 rounded-full" />
                  <div className="space-y-1.5">
                    <Skeleton className="h-4 w-28" />
                    <Skeleton className="h-3 w-20" />
                  </div>
                </div>
                <Skeleton className="h-8 w-20 rounded-lg" />
              </div>
            ))}
          </div>
        )}

        {isError && (
          <div className="text-center py-8 space-y-3">
            <AlertCircle className="h-8 w-8 text-destructive mx-auto" />
            <p className="text-sm font-medium text-foreground">Failed to load {tab}</p>
            <Button
              variant="outline"
              size="sm"
              onClick={() => activeQuery.refetch()}
            >
              Retry
            </Button>
          </div>
        )}

        {!isLoading && !isError && users.length === 0 && (
          <div className="text-center py-12 space-y-3">
            <div className="inline-flex h-12 w-12 items-center justify-center rounded-full bg-muted text-muted-foreground">
              <Users className="h-6 w-6" />
            </div>
            <p className="text-sm font-medium text-foreground">
              {tab === "followers" ? "No followers yet" : "Not following anyone yet"}
            </p>
            <p className="text-xs text-muted-foreground max-w-xs mx-auto">
              {tab === "followers"
                ? `@${targetUsername} doesn't have any followers yet.`
                : `@${targetUsername} isn't following anyone yet.`}
            </p>
          </div>
        )}

        {!isLoading && !isError && users.length > 0 && (
          <div className="space-y-4 divide-y divide-border/40">
            {users.map((user) => (
              <div
                key={user.id}
                className="flex items-center justify-between pt-3 first:pt-0"
              >
                <Link
                  href={`/user/${encodeURIComponent(user.username)}`}
                  onClick={onClose}
                  className="flex items-center space-x-3 group min-w-0 pr-2"
                >
                  <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground overflow-hidden">
                    {user.avatarUrl ? (
                      <img
                        src={user.avatarUrl}
                        alt={user.displayName || user.username}
                        className="h-full w-full object-cover"
                      />
                    ) : (
                      user.displayName?.charAt(0).toUpperCase() ||
                      user.username?.charAt(0).toUpperCase()
                    )}
                  </div>
                  <div className="min-w-0 flex flex-col justify-center">
                    <div className="flex items-center space-x-1.5 leading-tight">
                      <span className="font-bold text-sm text-foreground truncate group-hover:underline">
                        {user.displayName}
                      </span>
                      {user.isVerified && (
                        <CheckCircle2 className="h-3.5 w-3.5 text-blue-500 fill-blue-500/10 flex-shrink-0" />
                      )}
                      <FollowStatusBadge
                        isFollowing={user.isFollowing}
                        isFollowedBy={user.isFollowedBy}
                        isBlocked={user.isBlocked}
                      />
                    </div>
                    <p className="text-xs text-muted-foreground truncate leading-tight mt-0.5">
                      @{user.username}
                    </p>
                    {user.bio && (
                      <p className="text-xs text-foreground/90 mt-1 line-clamp-2 leading-relaxed">
                        {user.bio}
                      </p>
                    )}
                  </div>
                </Link>

                <FollowButton
                  targetUserId={user.id}
                  targetUsername={user.username}
                  isFollowing={user.isFollowing}
                  isBlocked={user.isBlocked}
                  size="sm"
                />
              </div>
            ))}

            {hasNextPage && (
              <div className="pt-4 text-center">
                <Button
                  variant="outline"
                  size="sm"
                  isLoading={isFetchingNextPage}
                  onClick={() => activeQuery.fetchNextPage()}
                  className="w-full"
                >
                  {isFetchingNextPage ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin mr-1.5" />
                      Loading more...
                    </>
                  ) : (
                    "Load more"
                  )}
                </Button>
              </div>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
}
