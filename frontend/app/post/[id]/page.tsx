"use client";

import React, { useState, useEffect, use } from "react";
import { useRouter } from "next/navigation";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, AlertCircle, RefreshCw } from "lucide-react";
import { PostCard } from "../../../components/posts/PostCard";
import { ReplyComposer } from "../../../components/posts/ReplyComposer";
import { ReplyThread } from "../../../components/posts/ReplyThread";
import { Skeleton } from "../../../components/ui/skeleton";
import { Button } from "../../../components/ui/button";
import { useRealtime, useRealtimeEvent } from "../../../hooks/useRealtime";
import { postsApi } from "../../../lib/api/posts";
import type { Post, ReplyItem as ReplyItemType } from "../../../types/api";

interface PostDetailPageProps {
  params: Promise<{
    id: string;
  }>;
}

export default function PostDetailPage({ params }: PostDetailPageProps) {
  const resolvedParams = use(params);
  const postId = resolvedParams.id;
  const router = useRouter();
  const queryClient = useQueryClient();
  const { joinPost, leavePost } = useRealtime();
  const [newReplies, setNewReplies] = useState<ReplyItemType[]>([]);

  // Join the post SignalR group on mount and leave on unmount
  useEffect(() => {
    if (postId) {
      joinPost(postId);
      return () => {
        leavePost(postId);
      };
    }
  }, [postId, joinPost, leavePost]);

  // Real-time synchronization for likes
  useRealtimeEvent("PostLiked", (data) => {
    if (data.postId === postId) {
      queryClient.setQueryData<Post>(["post-detail", postId], (old) => {
        if (!old) return old;
        return { ...old, likeCount: data.likeCount };
      });
    }
  });

  useRealtimeEvent("PostUnliked", (data) => {
    if (data.postId === postId) {
      queryClient.setQueryData<Post>(["post-detail", postId], (old) => {
        if (!old) return old;
        return { ...old, likeCount: data.likeCount };
      });
    }
  });

  // Real-time synchronization for incoming replies
  useRealtimeEvent("ReplyCreated", (data) => {
    if (data.postId === postId) {
      queryClient.setQueryData<Post>(["post-detail", postId], (old) => {
        if (!old) return old;
        return { ...old, replyCount: data.replyCount };
      });
      queryClient.invalidateQueries({ queryKey: ["post-replies", postId] });
    }
  });

  const {
    data: post,
    isLoading,
    isError,
    error,
    refetch,
    isFetching,
  } = useQuery<Post>({
    queryKey: ["post-detail", postId],
    queryFn: async () => {
      const res = await postsApi.getPost(postId);
      return res.data;
    },
  });

  // If the resolved post has a different ID (e.g. user navigated via a reply ID), update URL to the canonical post ID
  useEffect(() => {
    if (post && post.id !== postId) {
      router.replace(`/post/${post.id}`);
    }
  }, [post, postId, router]);

  const handleReplyCreated = (newReply: ReplyItemType) => {
    setNewReplies((prev) => [newReply, ...prev]);
    queryClient.invalidateQueries({ queryKey: ["post-detail", postId] });
    queryClient.invalidateQueries({ queryKey: ["post-replies", postId] });
    refetch();
  };

  if (isLoading) {
    return (
      <main className="max-w-3xl mx-auto px-4 py-8 space-y-6">
        <div className="flex items-center space-x-2">
          <Skeleton className="h-8 w-8 rounded-full" />
          <Skeleton className="h-6 w-32" />
        </div>
        <div className="rounded-2xl border border-border bg-card p-6 space-y-4">
          <div className="flex items-center space-x-3">
            <Skeleton className="h-12 w-12 rounded-full" />
            <div className="space-y-2">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-24" />
            </div>
          </div>
          <Skeleton className="h-16 w-full" />
          <Skeleton className="h-48 w-full rounded-xl" />
        </div>
      </main>
    );
  }

  if (isError || !post) {
    return (
      <main className="max-w-3xl mx-auto px-4 py-16 text-center space-y-4">
        <div className="inline-flex h-14 w-14 items-center justify-center rounded-2xl bg-destructive/10 text-destructive mb-2">
          <AlertCircle className="h-7 w-7" />
        </div>
        <h1 className="text-2xl font-bold text-foreground">Post Not Found</h1>
        <p className="text-sm text-muted-foreground max-w-md mx-auto">
          {error instanceof Error
            ? error.message
            : "The post you're looking for doesn't exist, has been deleted, or is unavailable."}
        </p>
        <div className="flex items-center justify-center space-x-3 pt-2">
          <Button variant="outline" size="sm" onClick={() => router.back()}>
            <ArrowLeft className="mr-1.5 h-4 w-4" />
            Go Back
          </Button>
          <Button variant="default" size="sm" onClick={() => refetch()} isLoading={isFetching}>
            <RefreshCw className="mr-1.5 h-4 w-4" />
            Retry
          </Button>
        </div>
      </main>
    );
  }

  return (
    <main className="max-w-3xl mx-auto px-4 py-6 sm:py-8 space-y-6">
      {/* Header navigation bar */}
      <div className="flex items-center space-x-3">
        <button
          type="button"
          onClick={() => router.back()}
          className="rounded-full p-2 text-muted-foreground hover:bg-muted hover:text-foreground transition focus:outline-none focus:ring-1 focus:ring-primary"
          aria-label="Go back"
        >
          <ArrowLeft className="h-5 w-5" />
        </button>
        <h1 className="text-lg font-bold text-foreground">Post</h1>
      </div>

      {/* Main Post Card */}
      <PostCard post={post} isDetailedView={true} />

      {/* Primary Reply Composer */}
      <section className="space-y-3" aria-label="Reply to post">
        <ReplyComposer
          postId={post.id}
          placeholder={`Reply to @${post.author.username}...`}
          onReplyCreated={handleReplyCreated}
        />
      </section>

      {/* Threaded Conversation Replies */}
      <ReplyThread
        postId={post.id}
        externalNewReplies={newReplies}
        onReplyCountChange={() => {
          refetch();
        }}
      />
    </main>
  );
}
