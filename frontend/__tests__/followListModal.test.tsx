import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { FollowListModal } from "../components/social/FollowListModal";
import { socialApi } from "../lib/api/social";

vi.mock("../lib/api/social", () => ({
  socialApi: {
    getFollowers: vi.fn(),
    getFollowing: vi.fn(),
  },
}));

vi.mock("../hooks/useToast", () => ({
  useToast: () => ({
    success: vi.fn(),
    error: vi.fn(),
  }),
}));

vi.mock("../hooks/useAuth", () => ({
  useAuth: () => ({
    user: { id: "current-user", username: "current" },
    isAuthenticated: true,
  }),
}));

describe("FollowListModal Component", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });

    vi.mocked(socialApi.getFollowers).mockResolvedValue({
      success: true,
      data: {
        items: [],
        nextCursor: null,
        hasNextPage: false,
        pageSize: 20,
      },
    });

    vi.mocked(socialApi.getFollowing).mockResolvedValue({
      success: true,
      data: {
        items: [],
        nextCursor: null,
        hasNextPage: false,
        pageSize: 20,
      },
    });
  });

  const renderModal = (props: React.ComponentProps<typeof FollowListModal>) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <FollowListModal {...props} />
      </QueryClientProvider>
    );
  };

  it("should open with 'following' tab selected when initialTab is 'following'", () => {
    renderModal({
      isOpen: true,
      onClose: vi.fn(),
      targetUserId: "user-alice",
      targetUsername: "alice",
      targetDisplayName: "Alice Walker",
      initialTab: "following",
    });

    const followingTab = screen.getByRole("tab", { name: /Following/i });
    expect(followingTab.getAttribute("aria-selected")).toBe("true");

    const followersTab = screen.getByRole("tab", { name: /Followers/i });
    expect(followersTab.getAttribute("aria-selected")).toBe("false");
  });

  it("should open with 'followers' tab selected when initialTab is 'followers'", () => {
    renderModal({
      isOpen: true,
      onClose: vi.fn(),
      targetUserId: "user-alice",
      targetUsername: "alice",
      targetDisplayName: "Alice Walker",
      initialTab: "followers",
    });

    const followersTab = screen.getByRole("tab", { name: /Followers/i });
    expect(followersTab.getAttribute("aria-selected")).toBe("true");

    const followingTab = screen.getByRole("tab", { name: /Following/i });
    expect(followingTab.getAttribute("aria-selected")).toBe("false");
  });

  it("should synchronize active tab when initialTab changes", () => {
    const { rerender } = renderModal({
      isOpen: true,
      onClose: vi.fn(),
      targetUserId: "user-alice",
      targetUsername: "alice",
      targetDisplayName: "Alice Walker",
      initialTab: "followers",
    });

    expect(screen.getByRole("tab", { name: /Followers/i }).getAttribute("aria-selected")).toBe("true");

    // Rerender with initialTab="following"
    rerender(
      <QueryClientProvider client={queryClient}>
        <FollowListModal
          isOpen={true}
          onClose={vi.fn()}
          targetUserId="user-alice"
          targetUsername="alice"
          targetDisplayName="Alice Walker"
          initialTab="following"
        />
      </QueryClientProvider>
    );

    expect(screen.getByRole("tab", { name: /Following/i }).getAttribute("aria-selected")).toBe("true");
  });
});
