"use client";

import React, { useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CheckCircle2, MessageSquare, Trash2, ArrowUpRight } from "lucide-react";
import { DeleteReplyModal } from "./DeleteReplyModal";
import { useAuth } from "../../hooks/useAuth";
import { formatPostTimestamp } from "../../lib/utils/time";
import type { UserReply } from "../../types/api";

interface UserReplyCardProps {
  reply: UserReply;
  onDeleted?: (replyId: string) => void;
  className?: string;
}

export function UserReplyCard({
  reply,
  onDeleted,
  className = "",
}: UserReplyCardProps) {
  const router = useRouter();
  const { user: currentUser } = useAuth();
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);

  const isOwner = !!(
    currentUser &&
    (currentUser.id === reply.author.id ||
      currentUser.username.toLowerCase() === reply.author.username.toLowerCase())
  );

  const formattedDate = formatPostTimestamp(reply.createdAt);

  const handleCardClick = (e: React.MouseEvent) => {
    const target = e.target as HTMLElement;
    if (
      target.closest("a") ||
      target.closest("button") ||
      target.tagName === "A" ||
      target.tagName === "BUTTON"
    ) {
      return;
    }
    router.push(`/post/${reply.postId}`);
  };

  return (
    <>
      <article
        onClick={handleCardClick}
        className={`cursor-pointer rounded-2xl border border-border bg-card p-4 sm:p-5 transition hover:border-border/80 hover:bg-card/90 shadow-sm ${className}`}
      >
        <div className="flex items-start space-x-3 sm:space-x-3.5">
          {/* Author Avatar */}
          <Link
            href={`/user/${encodeURIComponent(reply.author.username)}`}
            onClick={(e) => e.stopPropagation()}
            className="flex-shrink-0"
          >
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-primary font-bold text-primary-foreground overflow-hidden text-sm shadow-sm hover:opacity-90 transition">
              {reply.author.avatarUrl ? (
                <img
                  src={reply.author.avatarUrl}
                  alt={reply.author.displayName || reply.author.username}
                  className="h-full w-full object-cover"
                />
              ) : (
                reply.author.displayName?.charAt(0).toUpperCase() ||
                reply.author.username?.charAt(0).toUpperCase() ||
                "?"
              )}
            </div>
          </Link>

          {/* Body */}
          <div className="min-w-0 flex-1 space-y-1.5">
            {/* Author Header Info */}
            <div className="flex items-center justify-between">
              <div className="flex items-center space-x-1.5 truncate">
                <Link
                  href={`/user/${encodeURIComponent(reply.author.username)}`}
                  onClick={(e) => e.stopPropagation()}
                  className="font-bold text-foreground text-sm hover:underline truncate"
                >
                  {reply.author.displayName || reply.author.username}
                </Link>
                {reply.author.isVerified && (
                  <CheckCircle2 className="h-4 w-4 text-primary flex-shrink-0" />
                )}
                <span className="text-xs text-muted-foreground truncate">
                  @{reply.author.username}
                </span>
                <span className="text-xs text-muted-foreground">•</span>
                <span className="text-xs text-muted-foreground flex-shrink-0">
                  {formattedDate}
                </span>
              </div>

              {/* Action Menu (Delete if owner) */}
              {isOwner && (
                <button
                  type="button"
                  onClick={(e) => {
                    e.stopPropagation();
                    setIsDeleteModalOpen(true);
                  }}
                  className="rounded-full p-1 text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition"
                  title="Delete reply"
                  aria-label="Delete reply"
                >
                  <Trash2 className="h-4 w-4" />
                </button>
              )}
            </div>

            {/* Replying to Context */}
            {reply.replyingToUsername && (
              <p className="text-xs text-muted-foreground">
                Replying to{" "}
                <Link
                  href={`/user/${encodeURIComponent(reply.replyingToUsername)}`}
                  onClick={(e) => e.stopPropagation()}
                  className="text-primary hover:underline font-medium"
                >
                  @{reply.replyingToUsername}
                </Link>
              </p>
            )}

            {/* Reply Content */}
            <p className="text-sm leading-relaxed text-foreground whitespace-pre-wrap break-words pt-0.5">
              {reply.content}
            </p>

            {/* Link to Parent Post */}
            <div className="pt-2">
              <Link
                href={`/post/${reply.postId}`}
                onClick={(e) => e.stopPropagation()}
                className="inline-flex items-center space-x-1 text-xs font-medium text-primary hover:underline bg-primary/5 hover:bg-primary/10 px-2.5 py-1 rounded-lg transition"
              >
                <MessageSquare className="h-3.5 w-3.5" />
                <span>View in thread</span>
                <ArrowUpRight className="h-3 w-3 ml-0.5" />
              </Link>
            </div>
          </div>
        </div>
      </article>

      {/* Delete Reply Confirmation Modal */}
      <DeleteReplyModal
        replyId={reply.id}
        isOpen={isDeleteModalOpen}
        onClose={() => setIsDeleteModalOpen(false)}
        onDeleted={() => {
          setIsDeleteModalOpen(false);
          onDeleted?.(reply.id);
        }}
      />
    </>
  );
}
