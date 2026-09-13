import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { TrendingWidget } from "../components/trending/TrendingWidget";
import { trendsApi } from "../lib/api/trends";

vi.mock("../lib/api/trends", () => ({
  trendsApi: {
    getTrends: vi.fn(),
  },
}));

describe("TrendingWidget Component (Phase 12.3)", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
  });

  const renderWidget = (limit = 10) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <TrendingWidget limit={limit} />
      </QueryClientProvider>
    );
  };

  it("renders loading skeleton while fetching trending data", () => {
    vi.mocked(trendsApi.getTrends).mockReturnValue(new Promise(() => {})); // pending

    renderWidget();

    expect(screen.getByText("Tren untuk Anda")).toBeDefined();
    expect(screen.getByLabelText("Memuat tren")).toBeDefined();
  });

  it("renders top trending topics successfully with ranks and volume", async () => {
    vi.mocked(trendsApi.getTrends).mockResolvedValue({
      success: true,
      data: {
        items: [
          { rank: 1, tag: "cebas", score: 182.42, postCount: 245 },
          { rank: 2, tag: "technology", score: 151.83, postCount: 198 },
          { rank: 3, tag: "startup", score: 98.15, postCount: 120 },
        ],
      },
    });

    renderWidget();

    await waitFor(() => {
      expect(screen.getByText("#cebas")).toBeDefined();
    });

    expect(screen.getByText("#1")).toBeDefined();
    expect(screen.getByText("245 celotehan")).toBeDefined();

    expect(screen.getByText("#technology")).toBeDefined();
    expect(screen.getByText("#2")).toBeDefined();
    expect(screen.getByText("198 celotehan")).toBeDefined();

    expect(screen.getByText("#startup")).toBeDefined();
    expect(screen.getByText("#3")).toBeDefined();
    expect(screen.getByText("120 celotehan")).toBeDefined();

    // Verify navigation links
    const cebasLink = screen.getByRole("link", { name: /#1 #cebas 245 celotehan/i });
    expect(cebasLink.getAttribute("href")).toBe("/tag/cebas");
  });

  it("renders empty state when no trending topics exist", async () => {
    vi.mocked(trendsApi.getTrends).mockResolvedValue({
      success: true,
      data: { items: [] },
    });

    renderWidget();

    await waitFor(() => {
      expect(screen.getByText("Belum ada topik tren saat ini.")).toBeDefined();
    });
  });

  it("renders error state with retry button on API failure", async () => {
    vi.mocked(trendsApi.getTrends).mockRejectedValue(new Error("Network Error"));

    renderWidget();

    await waitFor(() => {
      expect(screen.getByText("Gagal memuat tren saat ini.")).toBeDefined();
    });

    const retryBtn = screen.getByRole("button", { name: /Coba Lagi/i });
    expect(retryBtn).toBeDefined();
  });
});
