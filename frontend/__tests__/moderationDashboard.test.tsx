import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen } from "@testing-library/react";
import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import ModerationPage from "../app/admin/moderation/page";
import { safetyApi } from "../lib/api/safety";
import * as useAuthModule from "../hooks/useAuth";

// Mock next/link
vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

// Mock safety API
vi.mock("../lib/api/safety", () => ({
  safetyApi: {
    getAdminReports: vi.fn(),
    executeModerationAction: vi.fn(),
    getSuspendedUsers: vi.fn(),
    unsuspendUser: vi.fn(),
  },
}));

// Mock useToast
vi.mock("../hooks/useToast", () => ({
  useToast: () => ({
    success: vi.fn(),
    error: vi.fn(),
  }),
}));

describe("Moderation Dashboard Page (/admin/moderation)", () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });
  });

  const renderWithProviders = (ui: React.ReactElement) => {
    return render(
      <QueryClientProvider client={queryClient}>
        {ui}
      </QueryClientProvider>
    );
  };

  it("should show Access Restricted when user is not staff", () => {
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: {
        id: "user-1",
        username: "regularuser",
        email: "user@test.com",
        displayName: "Regular User",
        role: "USER",
        createdAt: "2026-09-01",
      },
      isAuthenticated: true,
      isLoading: false,
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });

    renderWithProviders(<ModerationPage />);

    expect(screen.getByText(/Access Restricted/i)).toBeDefined();
    expect(screen.getByText(/The moderation operations dashboard is restricted/i)).toBeDefined();
  });

  it("should render moderation console and reports when user is MODERATOR", async () => {
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: {
        id: "mod-1",
        username: "moderator1",
        email: "mod@test.com",
        displayName: "Staff Moderator",
        role: "MODERATOR",
        createdAt: "2026-09-01",
      },
      isAuthenticated: true,
      isLoading: false,
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });

    vi.mocked(safetyApi.getAdminReports).mockResolvedValue({
      success: true,
      data: {
        items: [
          {
            id: "rep-101",
            reporterUserId: "user-9",
            reporterUsername: "whistleblower",
            reporterDisplayName: "Whistle Blower",
            targetType: "Post",
            targetPostId: "post-88",
            category: "SPAM",
            status: "PENDING",
            reason: "Crypto advertisement link",
            createdAt: new Date().toISOString(),
            targetPost: {
              id: "post-88",
              authorId: "author-2",
              authorUsername: "spammer",
              authorDisplayName: "Spam Bot",
              content: "Buy crypto fast at spam.example.com",
              mediaUrls: [],
              isDeleted: false,
              isHidden: false,
              createdAt: new Date().toISOString(),
            },
          },
        ],
        page: 1,
        pageSize: 15,
        totalCount: 1,
        totalPages: 1,
      },
    });

    renderWithProviders(<ModerationPage />);

    // Header title
    expect(await screen.findByText(/Moderation Operations/i)).toBeDefined();

    // Verify report item rendered in table
    expect(await screen.findByText(/@spammer/i)).toBeDefined();
    expect(screen.getByText(/Crypto advertisement link/i)).toBeDefined();
    expect(screen.getByRole("button", { name: /(View Report Details|Review Details)/i })).toBeDefined();
  });

  it("should render suspended users tab and allow unsuspending user", async () => {
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: {
        id: "admin-1",
        username: "admin",
        email: "admin@test.com",
        displayName: "Administrator",
        role: "ADMIN",
        createdAt: "2026-09-01",
      },
      isAuthenticated: true,
      isLoading: false,
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });

    vi.mocked(safetyApi.getAdminReports).mockResolvedValue({
      success: true,
      data: {
        items: [],
        page: 1,
        pageSize: 15,
        totalCount: 0,
        totalPages: 1,
      },
    });

    vi.mocked(safetyApi.getSuspendedUsers).mockResolvedValue({
      success: true,
      data: {
        items: [
          {
            id: "user-suspended-1",
            username: "spambot99",
            displayName: "Spam Bot 99",
            avatarUrl: null,
            role: "USER",
            suspendedAt: new Date().toISOString(),
            suspensionReason: "Terms of service violation",
            totalPosts: 5,
            createdAt: "2026-09-01",
          },
        ],
        page: 1,
        pageSize: 15,
        totalCount: 1,
        totalPages: 1,
      },
    });

    const { fireEvent } = await import("@testing-library/react");
    renderWithProviders(<ModerationPage />);

    // Switch to Suspended Users tab
    const suspendedTabButton = await screen.findByRole("button", { name: /Pengguna Ditangguhkan/i });
    fireEvent.click(suspendedTabButton);

    // Verify suspended user is listed
    expect(await screen.findByText(/@spambot99/i)).toBeDefined();
    expect(screen.getByText(/Terms of service violation/i)).toBeDefined();
    expect(screen.getByText(/5 Posts/i)).toBeDefined();

    // Verify Unsuspend button is present
    const unsuspendBtn = screen.getByRole("button", { name: /Aktifkan Kembali/i });
    expect(unsuspendBtn).toBeDefined();

    // Click Unsuspend to open modal
    fireEvent.click(unsuspendBtn);
    expect(screen.getByText(/Aktifkan Kembali Pengguna/i)).toBeDefined();
    expect(screen.getByPlaceholderText(/Tulis alasan pemulihan akun/i)).toBeDefined();
  });

  it("should render stacked reports with multiple reporters and inspect them in modal", async () => {
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: {
        id: "mod-1",
        username: "moderator1",
        email: "mod@test.com",
        displayName: "Staff Moderator",
        role: "MODERATOR",
        createdAt: "2026-09-01",
      },
      isAuthenticated: true,
      isLoading: false,
      login: vi.fn(),
      register: vi.fn(),
      logout: vi.fn(),
      refreshUser: vi.fn(),
    });

    vi.mocked(safetyApi.getAdminReports).mockResolvedValue({
      success: true,
      data: {
        items: [
          {
            id: "rep-stack-1",
            reporterUserId: "user-1",
            reporterUsername: "reporter_alice",
            reporterDisplayName: "Alice Reporter",
            targetType: "Post",
            targetPostId: "post-stacked-99",
            category: "SPAM",
            status: "PENDING",
            reason: "Initial spam advertisement",
            createdAt: new Date().toISOString(),
            reportCount: 2,
            categories: ["SPAM", "HARASSMENT"],
            reports: [
              {
                id: "rep-stack-1",
                reporterUserId: "user-1",
                reporterUsername: "reporter_alice",
                reporterDisplayName: "Alice Reporter",
                category: "SPAM",
                status: "PENDING",
                reason: "Initial spam advertisement",
                createdAt: new Date().toISOString(),
              },
              {
                id: "rep-stack-2",
                reporterUserId: "user-2",
                reporterUsername: "reporter_bob",
                reporterDisplayName: "Bob Reporter",
                category: "HARASSMENT",
                status: "PENDING",
                reason: "Abusive language in post",
                createdAt: new Date().toISOString(),
              },
            ],
            targetPost: {
              id: "post-stacked-99",
              authorId: "author-bad",
              authorUsername: "badactor",
              authorDisplayName: "Bad Actor",
              content: "Offensive content posted here",
              mediaUrls: [],
              isDeleted: false,
              isHidden: false,
              createdAt: new Date().toISOString(),
            },
          },
        ],
        page: 1,
        pageSize: 15,
        totalCount: 1,
        totalPages: 1,
      },
    });

    const { fireEvent } = await import("@testing-library/react");
    renderWithProviders(<ModerationPage />);

    // Verify stack badge rendered
    expect(await screen.findByText(/Stack: 2 Laporan/i)).toBeDefined();
    expect(screen.getByText(/2 Pelapor/i)).toBeDefined();

    // Verify Review Details button with count
    const reviewBtn = screen.getByRole("button", { name: /(View Report Details|Review Details) \(2 Laporan\)/i });
    expect(reviewBtn).toBeDefined();

    // Open review modal
    fireEvent.click(reviewBtn);

    // Verify modal displays all reporters
    expect(await screen.findByText(/Daftar Pelapor \(2 Laporan\)/i)).toBeDefined();
    expect(screen.getAllByText(/@reporter_alice/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getAllByText(/Initial spam advertisement/i).length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText(/@reporter_bob/i)).toBeDefined();
    expect(screen.getByText(/Abusive language in post/i)).toBeDefined();
  });
});
