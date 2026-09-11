import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { SearchHighlight } from "../components/search/SearchHighlight";
import { SearchBar } from "../components/search/SearchBar";
import { searchApi } from "../lib/api/search";
import { apiClient } from "../lib/api/client";

// Mock next/navigation
const mockPush = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({
    push: mockPush,
  }),
  useSearchParams: () => new URLSearchParams("q=test&tab=semua"),
}));

// Mock useAuth
vi.mock("../hooks/useAuth", () => ({
  useAuth: () => ({
    user: { id: "current-user-1", username: "myuser" },
    isAuthenticated: true,
  }),
}));

describe("Phase 11 Search Subsystem Frontend Tests", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
  });

  const renderWithProvider = (ui: React.ReactElement) => {
    return render(
      <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>
    );
  };

  describe("SearchHighlight Component (Zero-XSS Protection)", () => {
    it("should render plain text without highlight tags safely", () => {
      const { container } = render(
        <SearchHighlight text="Halo dunia CEBAS" />
      );
      expect(container.textContent).toBe("Halo dunia CEBAS");
      expect(container.querySelector("mark")).toBeNull();
    });

    it("should parse <mark> tags into styled <mark> elements", () => {
      const { container } = render(
        <SearchHighlight text="Sedang belajar <mark>Elasticsearch</mark> di CEBAS" />
      );
      expect(container.textContent).toBe("Sedang belajar Elasticsearch di CEBAS");
      const mark = container.querySelector("mark");
      expect(mark).not.toBeNull();
      expect(mark?.textContent).toBe("Elasticsearch");
    });

    it("should parse multiple highlight tags correctly", () => {
      const { container } = render(
        <SearchHighlight text="<mark>Celoteh</mark> seru tentang <highlight>kucing</highlight> lucu" />
      );
      const marks = container.querySelectorAll("mark");
      expect(marks.length).toBe(2);
      expect(marks[0].textContent).toBe("Celoteh");
      expect(marks[1].textContent).toBe("kucing");
    });

    it("should escape malicious HTML and script tags safely without execution", () => {
      const maliciousPayload = "<script>alert('xss')</script> Halo <mark>aman</mark>";
      const { container } = render(
        <SearchHighlight text={maliciousPayload} />
      );

      // Verify no executable <script> element was inserted into the DOM
      expect(container.querySelector("script")).toBeNull();
      // Text content must contain the escaped string
      expect(container.textContent).toContain("<script>alert('xss')</script>");
      // Highlighted term is properly enclosed in mark
      expect(container.querySelector("mark")?.textContent).toBe("aman");
    });
  });

  describe("searchApi Client", () => {
    it("should call /api/v1/search/posts with correct query and cursor parameters", async () => {
      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: { items: [], hasNextPage: false, pageSize: 20 },
      });

      await searchApi.searchPosts({ query: "indonesia", cursor: "cursor_123", limit: 20 });

      expect(getSpy).toHaveBeenCalledWith("/api/v1/search/posts", {
        params: {
          q: "indonesia",
          cursor: "cursor_123",
          limit: 20,
        },
      });
    });

    it("should call /api/v1/search/users with handle or name query", async () => {
      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: [],
      });

      await searchApi.searchUsers({ query: "developer", limit: 10 });

      expect(getSpy).toHaveBeenCalledWith("/api/v1/search/users", {
        params: {
          q: "developer",
          limit: 10,
        },
      });
    });

    it("should call /api/v1/search for combined discovery summary", async () => {
      const getSpy = vi.spyOn(apiClient, "get").mockResolvedValueOnce({
        success: true,
        data: { posts: [], users: [] },
      });

      await searchApi.searchSummary({ query: "cebas", postLimit: 5, userLimit: 5 });

      expect(getSpy).toHaveBeenCalledWith("/api/v1/search", {
        params: {
          q: "cebas",
          postLimit: 5,
          userLimit: 5,
        },
      });
    });
  });

  describe("SearchBar Component", () => {
    it("should render search input with appropriate accessibility attributes", () => {
      renderWithProvider(<SearchBar />);
      const input = screen.getByRole("searchbox", { name: /Cari di CEBAS/i });
      expect(input).toBeDefined();
      expect(input.getAttribute("placeholder")).toContain("Cari celotehan, akun");
    });

    it("should clear input when clear button is clicked", () => {
      renderWithProvider(<SearchBar initialQuery="testing" />);
      const clearBtn = screen.getByRole("button", { name: /Hapus pencarian/i });
      expect(clearBtn).toBeDefined();

      fireEvent.click(clearBtn);

      const input = screen.getByRole("searchbox") as HTMLInputElement;
      expect(input.value).toBe("");
    });

    it("should navigate to /search on Enter keypress", () => {
      renderWithProvider(<SearchBar />);
      const input = screen.getByRole("searchbox");

      fireEvent.change(input, { target: { value: "golang" } });
      fireEvent.keyDown(input, { key: "Enter" });

      expect(mockPush).toHaveBeenCalledWith("/search?q=golang");
    });
  });
});
