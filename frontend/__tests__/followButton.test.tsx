import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { FollowButton } from "../components/social/FollowButton";
import { socialApi } from "../lib/api/social";

vi.mock("../lib/api/social", () => ({
  socialApi: {
    followUser: vi.fn(),
    unfollowUser: vi.fn(),
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
    user: { id: "current-user-1", username: "currentuser" },
    isAuthenticated: true,
  }),
}));

describe("FollowButton Component", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });

    vi.mocked(socialApi.followUser).mockResolvedValue({
      success: true,
      data: { targetUserId: "target-user-1", isFollowing: true, isBlocked: false },
    });

    vi.mocked(socialApi.unfollowUser).mockResolvedValue({
      success: true,
      data: { targetUserId: "target-user-1", isFollowing: false, isBlocked: false },
    });
  });

  const renderComponent = (props: React.ComponentProps<typeof FollowButton>) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <FollowButton {...props} />
      </QueryClientProvider>
    );
  };

  it("renders 'Follow' button initially when isFollowing is false", () => {
    renderComponent({
      targetUserId: "target-user-1",
      targetUsername: "targetuser",
      isFollowing: false,
    });

    expect(screen.getByRole("button", { name: /Follow @targetuser/i })).toBeDefined();
    expect(screen.getByText("Follow")).toBeDefined();
  });

  it("renders 'Following' button initially when isFollowing is true", () => {
    renderComponent({
      targetUserId: "target-user-1",
      targetUsername: "targetuser",
      isFollowing: true,
    });

    expect(screen.getByRole("button", { name: /Following @targetuser/i })).toBeDefined();
    expect(screen.getByText("Following")).toBeDefined();
  });

  it("toggles to 'Following' optimistically upon clicking 'Follow'", async () => {
    const onFollowChange = vi.fn();

    renderComponent({
      targetUserId: "target-user-1",
      targetUsername: "targetuser",
      isFollowing: false,
      onFollowChange,
    });

    const button = screen.getByRole("button", { name: /Follow @targetuser/i });
    fireEvent.click(button);

    // Optimistically switches to Following
    expect(screen.getByText("Following")).toBeDefined();
    expect(onFollowChange).toHaveBeenCalledWith(true);
    await waitFor(() => {
      expect(socialApi.followUser).toHaveBeenCalledWith("target-user-1");
    });
  });

  it("shows 'Unfollow' on hover and toggles back to 'Follow' upon clicking", async () => {
    const onFollowChange = vi.fn();

    renderComponent({
      targetUserId: "target-user-1",
      targetUsername: "targetuser",
      isFollowing: true,
      onFollowChange,
    });

    const button = screen.getByRole("button", { name: /Following @targetuser/i });

    // Hover reveals Unfollow
    fireEvent.mouseEnter(button);
    expect(screen.getByText("Unfollow")).toBeDefined();

    // Click Unfollow
    fireEvent.click(button);
    expect(screen.getByText("Follow")).toBeDefined();
    expect(onFollowChange).toHaveBeenCalledWith(false);
    await waitFor(() => {
      expect(socialApi.unfollowUser).toHaveBeenCalledWith("target-user-1");
    });
  });

  it("supports initialIsFollowing prop as alias", () => {
    renderComponent({
      targetUserId: "target-user-1",
      targetUsername: "targetuser",
      initialIsFollowing: true,
    });

    expect(screen.getByText("Following")).toBeDefined();
  });

  it("does not render when targetUserId is the current user", () => {
    const { container } = renderComponent({
      targetUserId: "current-user-1",
      targetUsername: "currentuser",
      isFollowing: false,
    });

    expect(container.firstChild).toBeNull();
  });
});
