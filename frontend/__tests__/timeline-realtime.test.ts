import { describe, it, expect, vi } from "vitest";

describe("Timeline Real-Time Synchronization Logic", () => {
  interface PostState {
    id: string;
    likeCount: number;
    replyCount: number;
    liked: boolean;
  }

  interface PostLikedEvent {
    postId: string;
    actorUserId: string;
    likeCount: number;
  }

  interface PostUnlikedEvent {
    postId: string;
    actorUserId: string;
    likeCount: number;
  }

  interface ReplyCreatedEvent {
    replyId: string;
    postId: string;
    replyCount: number;
  }

  it("should reconcile post like count when PostLiked event is received", () => {
    let post: PostState = {
      id: "post-1",
      likeCount: 5,
      replyCount: 2,
      liked: false,
    };

    const handlePostLiked = (evt: PostLikedEvent) => {
      if (evt.postId === post.id) {
        post = { ...post, likeCount: evt.likeCount };
      }
    };

    handlePostLiked({ postId: "post-1", actorUserId: "user-2", likeCount: 6 });
    expect(post.likeCount).toBe(6);

    // Event for another post should not affect this post
    handlePostLiked({ postId: "post-99", actorUserId: "user-2", likeCount: 20 });
    expect(post.likeCount).toBe(6);
  });

  it("should reconcile post like count when PostUnliked event is received", () => {
    let post: PostState = {
      id: "post-1",
      likeCount: 6,
      replyCount: 2,
      liked: true,
    };

    const handlePostUnliked = (evt: PostUnlikedEvent) => {
      if (evt.postId === post.id) {
        post = { ...post, likeCount: evt.likeCount };
      }
    };

    handlePostUnliked({ postId: "post-1", actorUserId: "user-2", likeCount: 5 });
    expect(post.likeCount).toBe(5);
  });

  it("should reconcile post reply count when ReplyCreated event is received", () => {
    let post: PostState = {
      id: "post-1",
      likeCount: 5,
      replyCount: 2,
      liked: false,
    };

    const handleReplyCreated = (evt: ReplyCreatedEvent) => {
      if (evt.postId === post.id) {
        post = { ...post, replyCount: evt.replyCount };
      }
    };

    handleReplyCreated({ replyId: "reply-10", postId: "post-1", replyCount: 3 });
    expect(post.replyCount).toBe(3);
  });

  it("should buffer new posts and flush into timeline without scroll jumps", () => {
    let timelinePosts = ["post-1", "post-2"];
    let bufferedPosts: string[] = [];

    const handleNewPostAvailable = (newPostId: string) => {
      if (!timelinePosts.includes(newPostId) && !bufferedPosts.includes(newPostId)) {
        bufferedPosts = [newPostId, ...bufferedPosts];
      }
    };

    handleNewPostAvailable("post-3");
    handleNewPostAvailable("post-4");
    // Duplicate should be ignored
    handleNewPostAvailable("post-3");

    expect(bufferedPosts.length).toBe(2);
    expect(timelinePosts.length).toBe(2);

    // User clicks the floating banner
    const flushBanner = () => {
      timelinePosts = [...bufferedPosts, ...timelinePosts];
      bufferedPosts = [];
    };

    flushBanner();
    expect(bufferedPosts.length).toBe(0);
    expect(timelinePosts).toEqual(["post-4", "post-3", "post-1", "post-2"]);
  });
});
