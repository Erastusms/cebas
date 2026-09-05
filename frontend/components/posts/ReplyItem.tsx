"use client";

import React, { useState } from "react";
import Link from "next/link";
import { Trash2, CheckCircle2, CornerDownRight } from "lucide-react";
import { ReplyComposer } from "./ReplyComposer";
import { DeleteReplyModal } from "./DeleteReplyModal";
import { useAuth } from "../../hooks/useAuth";
import { formatPostTimestamp } from "../../lib/utils/time";
import type { ReplyItem as ReplyItemType } from "../../types/api";

interface ReplyItemProps {
  reply: ReplyItemType;
  postId: string;
  onReplyAdded?: (newReply: ReplyItemType) => void;
  onReplyDeleted?: (replyId: string) => void;
}

export function ReplyItem({
  reply,
  postId,
  onReplyAdded,
  onReplyDeleted,
}: ReplyItemProps) {
  const { user: currentUser } = useAuth();
  const [isReplying, setIsReplying] = useState(false);
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);

  const isOwner = !!(
    currentUser &&
    reply.author &&
    (currentUser.id === reply.author.id ||
      currentUser.username.toLowerCase() === reply.author.username.toLowerCase())
  );

  const formattedDate = formatPostTimestamp(reply.createdAt);

  // Calculate indentation according to depth (max visual cap at depth 4 to prevent mobile layout squeeze)
  const visualDepth = Math.min(reply.depth, 4);

  return (
    <div
      className={`relative transition ${
        visualDepth > 0 ? "ml-3 sm:ml-6 border-l-2 border-border/80 pl-3 sm:pl-4 pt-2" : "pt-3"
      }`}
    >
      <div className="rounded-xl border border-border/60 bg-card/60 p-3.5 sm:p-4 shadow-sm">
        {reply.isDeleted ? (
          // Soft-deleted state
          <div className="text-xs italic text-muted-foreground py-1">
            <span>{reply.content || "[This reply was deleted by the author]"}</span>
          </div>
        ) : (
          // Active reply state
          <>
            <div className="flex items-start justify-between">
              {/* Author Info */}
              <div className="flex items-center space-x-2.5">
                {reply.author ? (
                  <Link
                    href={`/user/${encodeURIComponent(reply.author.username)}`}
                    className="flex-shrink-0"
                  >
                    <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary text-primary-foreground text-xs font-bold overflow-hidden shadow-sm">
                      {reply.author.avatarUrl ? (
                        <img
                          src={reply.author.avatarUrl}
                          alt={reply.author.displayName || reply.author.username}
                          className="h-full w-full object-cover"
                        />
                      ) : (
                        reply.author.displayName?.charAt(0).toUpperCase() ||
                        reply.author.username?.charAt(0).toUpperCase()
                      )}
                    </div>
                  </Link>
                ) : (
                  <div className="flex h-8 w-8 items-center justify-center rounded-full bg-muted text-muted-foreground text-xs font-bold">
                    ?
                  </div>
                )}

                <div>
                  <div className="flex items-center space-x-1.5 leading-tight">
                    {reply.author ? (
                      <Link
                        href={`/user/${encodeURIComponent(reply.author.username)}`}
                        className="text-xs sm:text-sm font-semibold text-foreground hover:underline"
                      >
                        {reply.author.displayName}
                      </Link>
                    ) : (
                      <span className="text-xs sm:text-sm font-semibold text-muted-foreground">
                        User
                      </span>
                    )}

                    {reply.author?.isVerified && (
                      <CheckCircle2 className="h-3 w-3 sm:h-3.5 sm:w-3.5 text-blue-500 fill-blue-500/10" aria-label="Verified" />
                    )}
                  </div>
                  <div className="flex items-center space-x-1.5 text-[11px] text-muted-foreground">
                    {reply.author && (
                      <Link
                        href={`/user/${encodeURIComponent(reply.author.username)}`}
                        className="hover:underline"
                      >
                        @{reply.author.username}
                      </Link>
                    )}
                    <span>•</span>
                    <time dateTime={reply.createdAt}>{formattedDate}</time>
                  </div>
                </div>
              </div>

              {/* Owner Delete Action */}
              {isOwner && (
                <button
                  type="button"
                  onClick={() => setIsDeleteModalOpen(true)}
                  className="rounded p-1 text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition focus:outline-none focus:ring-1 focus:ring-destructive"
                  aria-label="Delete reply"
                >
                  <Trash2 className="h-3.5 w-3.5" />
                </button>
              )}
            </div>

            {/* Comment Text Content */}
            <p className="mt-2.5 whitespace-pre-wrap text-xs sm:text-sm leading-relaxed text-foreground/90">
              {reply.content}
            </p>

            {/* Action Bar */}
            <div className="mt-2.5 flex items-center space-x-3 border-t border-border/40 pt-2 text-xs">
              <button
                type="button"
                onClick={() => setIsReplying((prev) => !prev)}
                className="flex items-center space-x-1 text-muted-foreground hover:text-primary transition"
                aria-label="Reply to comment"
              >
                <CornerDownRight className="h-3.5 w-3.5" />
                <span className="font-medium">Reply</span>
              </button>
            </div>
          </>
        )}
      </div>

      {/* Inline Nested Reply Composer */}
      {isReplying && (
        <div className="mt-2">
          <ReplyComposer
            postId={postId}
            parentReplyId={reply.id}
            replyingToUsername={reply.author?.username}
            onReplyCreated={(newRep) => {
              setIsReplying(false);
              if (onReplyAdded) onReplyAdded(newRep);
            }}
            onCancel={() => setIsReplying(false)}
          />
        </div>
      )}

      {/* Delete Confirmation Modal */}
      {isOwner && (
        <DeleteReplyModal
          isOpen={isDeleteModalOpen}
          onClose={() => setIsDeleteModalOpen(false)}
          replyId={reply.id}
          onDeleted={() => {
            if (onReplyDeleted) onReplyDeleted(reply.id);
          }}
        />
      )}
    </div>
  );
}
