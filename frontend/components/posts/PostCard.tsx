"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { MessageSquare, MoreHorizontal, Trash2, CheckCircle2 } from "lucide-react";
import { PostMediaGrid } from "./PostMediaGrid";
import { DeletePostModal } from "./DeletePostModal";
import { LikeButton } from "./LikeButton";
import { BookmarkButton } from "./BookmarkButton";
import { useAuth } from "../../hooks/useAuth";
import { formatPostTimestamp } from "../../lib/utils/time";
import type { Post } from "../../types/api";

interface PostCardProps {
  post: Post;
  onDeleted?: (postId: string) => void;
  className?: string;
  isDetailedView?: boolean;
}

export function PostCard({
  post,
  onDeleted,
  className = "",
  isDetailedView = false,
}: PostCardProps) {
  const router = useRouter();
  const { user: currentUser } = useAuth();
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [isMenuOpen, setIsMenuOpen] = useState(false);

  const isOwner = !!(
    currentUser &&
    (currentUser.id === post.author.id ||
      currentUser.username.toLowerCase() === post.author.username.toLowerCase())
  );

  const formattedDate = formatPostTimestamp(post.createdAt);

  const handleCardClick = (e: React.MouseEvent) => {
    // If not clicking a link, button, or media image, navigate to detailed view
    const target = e.target as HTMLElement;
    if (
      !isDetailedView &&
      !target.closest("a") &&
      !target.closest("button") &&
      !target.closest("img")
    ) {
      router.push(`/post/${post.id}`);
    }
  };

  return (
    <>
      <article
        onClick={handleCardClick}
        className={`rounded-2xl border border-border bg-card p-4 sm:p-6 shadow-sm transition ${
          !isDetailedView ? "hover:border-primary/40 cursor-pointer" : ""
        } ${className}`}
      >
        <div className="flex items-start justify-between">
          {/* Author Header */}
          <div className="flex items-center space-x-3">
            <Link
              href={`/user/${encodeURIComponent(post.author.username)}`}
              className="flex-shrink-0"
              onClick={(e) => e.stopPropagation()}
            >
              <div className="flex h-10 w-10 sm:h-11 sm:w-11 items-center justify-center rounded-full bg-primary text-primary-foreground font-bold overflow-hidden shadow-sm">
                {post.author.avatarUrl ? (
                  <img
                    src={post.author.avatarUrl}
                    alt={post.author.displayName || post.author.username}
                    className="h-full w-full object-cover"
                  />
                ) : (
                  post.author.displayName?.charAt(0).toUpperCase() ||
                  post.author.username?.charAt(0).toUpperCase()
                )}
              </div>
            </Link>

            <div>
              <div className="flex items-center space-x-1.5 leading-tight">
                <Link
                  href={`/user/${encodeURIComponent(post.author.username)}`}
                  className="text-sm sm:text-base font-bold text-foreground hover:underline"
                  onClick={(e) => e.stopPropagation()}
                >
                  {post.author.displayName}
                </Link>
                {post.author.isVerified && (
                  <CheckCircle2 className="h-3.5 w-3.5 sm:h-4 sm:w-4 text-blue-500 fill-blue-500/10" aria-label="Verified" />
                )}
              </div>
              <div className="flex items-center space-x-2 text-xs text-muted-foreground">
                <Link
                  href={`/user/${encodeURIComponent(post.author.username)}`}
                  className="hover:underline"
                  onClick={(e) => e.stopPropagation()}
                >
                  @{post.author.username}
                </Link>
                <span>•</span>
                <time dateTime={post.createdAt}>{formattedDate}</time>
              </div>
            </div>
          </div>

          {/* Owner Action Dropdown */}
          {isOwner && (
            <div className="relative">
              <button
                type="button"
                onClick={(e) => {
                  e.stopPropagation();
                  setIsMenuOpen((prev) => !prev);
                }}
                className="rounded-full p-1.5 text-muted-foreground hover:bg-muted hover:text-foreground focus:outline-none focus:ring-1 focus:ring-primary"
                aria-label="Post actions"
              >
                <MoreHorizontal className="h-4 w-4" />
              </button>

              {isMenuOpen && (
                <div
                  className="absolute right-0 z-20 mt-1 w-36 rounded-xl border border-border bg-popover p-1 shadow-lg"
                  onClick={(e) => e.stopPropagation()}
                >
                  <button
                    type="button"
                    onClick={() => {
                      setIsMenuOpen(false);
                      setIsDeleteModalOpen(true);
                    }}
                    className="flex w-full items-center space-x-2 rounded-lg px-2.5 py-1.5 text-xs font-medium text-destructive hover:bg-destructive/10 transition"
                  >
                    <Trash2 className="h-3.5 w-3.5" />
                    <span>Delete Post</span>
                  </button>
                </div>
              )}
            </div>
          )}
        </div>

        {/* Post Text Content */}
        {post.content && (
          <p className="mt-3 whitespace-pre-wrap text-sm sm:text-base leading-relaxed text-foreground/95">
            {post.content}
          </p>
        )}

        {/* Media Grid */}
        {post.media && post.media.length > 0 && (
          <div className="mt-3" onClick={(e) => e.stopPropagation()}>
            <PostMediaGrid media={post.media} />
          </div>
        )}

        {/* Engagement Footer */}
        <div className="mt-4 flex items-center justify-between border-t border-border/70 pt-3 text-xs text-muted-foreground">
          <div className="flex items-center space-x-4">
            <LikeButton
              postId={post.id}
              initialLiked={post.liked}
              initialLikeCount={post.likeCount}
            />

            <Link
              href={`/post/${post.id}`}
              onClick={(e) => e.stopPropagation()}
              className="flex items-center space-x-1.5 rounded-md px-2 py-1 hover:bg-muted hover:text-primary transition"
            >
              <MessageSquare className="h-4 w-4" />
              <span>{post.replyCount} Replies</span>
            </Link>
          </div>

          <div className="flex items-center space-x-1">
            <BookmarkButton
              postId={post.id}
              initialBookmarked={post.bookmarked}
              initialBookmarkCount={post.bookmarkCount}
            />
          </div>
        </div>
      </article>

      {/* Delete Confirmation Modal */}
      {isOwner && (
        <DeletePostModal
          isOpen={isDeleteModalOpen}
          onClose={() => setIsDeleteModalOpen(false)}
          postId={post.id}
          onDeleted={() => {
            if (onDeleted) onDeleted(post.id);
            if (isDetailedView) {
              router.push("/");
            }
          }}
        />
      )}
    </>
  );
}
