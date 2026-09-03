"use client";

import React, { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { MessageSquare, RefreshCw, AlertCircle } from "lucide-react";
import { ReplyItem } from "./ReplyItem";
import { Button } from "../ui/button";
import { Skeleton } from "../ui/skeleton";
import { postsApi } from "../../lib/api/posts";
import type { ReplyItem as ReplyItemType, HierarchicalRepliesResult } from "../../types/api";

interface ReplyThreadProps {
  postId: string;
  className?: string;
  externalNewReplies?: ReplyItemType[];
  onReplyCountChange?: (count: number) => void;
}

export function ReplyThread({
  postId,
  className = "",
  externalNewReplies = [],
  onReplyCountChange,
}: ReplyThreadProps) {
  const [extraItems, setExtraItems] = useState<ReplyItemType[]>([]);
  const [deletedIds, setDeletedIds] = useState<Set<string>>(new Set());

  // Replies Query via TanStack Query
  const {
    data,
    isLoading,
    isError,
    error,
    refetch,
    isFetching,
  } = useQuery<HierarchicalRepliesResult>({
    queryKey: ["post-replies", postId],
    queryFn: async () => {
      const res = await postsApi.getReplies(postId, null, 100);
      return res.data;
    },
    staleTime: 0,
    refetchOnWindowFocus: true,
  });

  const handleReplyAdded = (newReply: ReplyItemType) => {
    setExtraItems((prev) => [...prev, newReply]);
    if (onReplyCountChange && data) {
      onReplyCountChange(data.items.length + extraItems.length + 1);
    }
  };

  const handleReplyDeleted = (replyId: string) => {
    setDeletedIds((prev) => new Set([...prev, replyId]));
    refetch();
  };

  if (isLoading) {
    return (
      <div className={`space-y-4 pt-4 ${className}`}>
        <div className="flex items-center space-x-2">
          <Skeleton className="h-4 w-24" />
        </div>
        <div className="space-y-3">
          <div className="rounded-xl border border-border/60 p-4 space-y-2">
            <div className="flex items-center space-x-2">
              <Skeleton className="h-7 w-7 rounded-full" />
              <Skeleton className="h-4 w-32" />
            </div>
            <Skeleton className="h-4 w-full" />
          </div>
          <div className="ml-6 rounded-xl border border-border/60 p-4 space-y-2">
            <div className="flex items-center space-x-2">
              <Skeleton className="h-7 w-7 rounded-full" />
              <Skeleton className="h-4 w-28" />
            </div>
            <Skeleton className="h-4 w-3/4" />
          </div>
        </div>
      </div>
    );
  }

  if (isError) {
    return (
      <div className={`rounded-xl border border-destructive/20 bg-destructive/5 p-6 text-center space-y-3 ${className}`}>
        <AlertCircle className="mx-auto h-8 w-8 text-destructive" />
        <h4 className="text-sm font-semibold text-foreground">Failed to load replies</h4>
        <p className="text-xs text-muted-foreground">
          {error instanceof Error ? error.message : "Could not retrieve conversation replies."}
        </p>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => refetch()}
          isLoading={isFetching}
        >
          <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
          Retry
        </Button>
      </div>
    );
  }

  // Combine query replies with newly added local and external replies
  const serverItems = data?.items || [];
  const allDisplayItems = [...extraItems, ...externalNewReplies, ...serverItems].filter(
    (item, index, self) => self.findIndex((i) => i.id === item.id) === index
  );

  const visibleItems = allDisplayItems.map((item) =>
    deletedIds.has(item.id)
      ? { ...item, isDeleted: true, content: "[This reply was deleted by the author]", author: null }
      : item
  );

  const handleRefresh = async () => {
    setExtraItems([]);
    await refetch();
  };

  return (
    <section className={`space-y-4 ${className}`} aria-label="Conversation Replies">
      <div className="flex items-center justify-between border-b border-border pb-2">
        <h3 className="text-sm font-bold text-foreground">
          Replies ({visibleItems.filter((r) => !r.isDeleted).length})
        </h3>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          onClick={handleRefresh}
          disabled={isFetching}
          className="text-xs text-muted-foreground hover:text-foreground h-7 px-2"
        >
          <RefreshCw className={`mr-1 h-3 w-3 ${isFetching ? "animate-spin" : ""}`} />
          <span>Refresh</span>
        </Button>
      </div>

      {visibleItems.length === 0 ? (
        <div className="rounded-2xl border border-border/80 bg-muted/10 p-8 text-center space-y-2">
          <MessageSquare className="mx-auto h-8 w-8 text-muted-foreground/60" />
          <p className="text-sm font-medium text-foreground">No replies yet</p>
          <p className="text-xs text-muted-foreground">
            Be the first to join the conversation!
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {visibleItems.map((reply) => (
            <ReplyItem
              key={reply.id}
              reply={reply}
              postId={postId}
              onReplyAdded={handleReplyAdded}
              onReplyDeleted={handleReplyDeleted}
            />
          ))}
        </div>
      )}
    </section>
  );
}
