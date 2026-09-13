import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TagTimelineClient } from "../components/hashtags/TagTimelineClient";
import { timelinesApi } from "../lib/api/timelines";

const mockBack = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({
    back: mockBack,
    push: vi.fn(),
  }),
}));

vi.mock("../lib/api/timelines", () => ({
  timelinesApi: {
    getTagTimeline: vi.fn(),
  },
}));

vi.mock("../lib/api/trends", () => ({
  trendsApi: {
    getTrends: vi.fn().mockResolvedValue({ success: true, data: { items: [] } }),
  },
}));

// Mock IntersectionObserver for InfiniteFeed in jsdom
class MockIntersectionObserver {
  observe = vi.fn();
  unobserve = vi.fn();
  disconnect = vi.fn();
}

beforeEach(() => {
  window.IntersectionObserver = MockIntersectionObserver as unknown as typeof IntersectionObserver;
});

describe("TagTimelineClient Component (Phase 12.4)", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
  });

  const renderClient = (tag = "cebas") => {
    return render(
      <QueryClientProvider client={queryClient}>
        <TagTimelineClient tag={tag} />
      </QueryClientProvider>
    );
  };

  it("renders tag header with topic name and back button", async () => {
    vi.mocked(timelinesApi.getTagTimeline).mockResolvedValue({
      success: true,
      data: {
        items: [],
        nextCursor: null,
        hasNextPage: false,
        pageSize: 20,
      },
    });

    renderClient("cebas");

    expect(screen.getByRole("heading", { level: 1 }).textContent).toContain("#cebas");
    expect(screen.getByText("Linimasa Topik")).toBeDefined();
    expect(screen.getByRole("button", { name: /Kembali/i })).toBeDefined();
    expect(screen.getByRole("button", { name: /Segarkan linimasa topik/i })).toBeDefined();
  });

  it("renders empty state when tag has no posts", async () => {
    vi.mocked(timelinesApi.getTagTimeline).mockResolvedValue({
      success: true,
      data: {
        items: [],
        nextCursor: null,
        hasNextPage: false,
        pageSize: 20,
      },
    });

    renderClient("unknown_topic");

    await waitFor(() => {
      expect(screen.getByText("Belum ada celotehan tentang #unknown_topic")).toBeDefined();
    });
  });
});
