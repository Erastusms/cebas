import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor, act } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ThemeSwitcher } from "../components/ui/ThemeSwitcher";
import { ThemeProvider, useThemeContext } from "../providers/ThemeProvider";
import { usersApi } from "../lib/api/users";
import * as useAuthModule from "../hooks/useAuth";
import * as useToastModule from "../hooks/useToast";

// Mock next-themes
let currentMockTheme = "system";
const mockSetTheme = vi.fn((newTheme: string) => {
  currentMockTheme = newTheme;
});

vi.mock("next-themes", () => ({
  ThemeProvider: ({ children }: { children: React.ReactNode }) => <div data-testid="next-themes-provider">{children}</div>,
  useTheme: () => ({
    theme: currentMockTheme,
    resolvedTheme: currentMockTheme === "system" ? "light" : currentMockTheme,
    setTheme: mockSetTheme,
  }),
}));

vi.mock("../lib/api/users", () => ({
  usersApi: {
    updateProfile: vi.fn(),
  },
}));

describe("Theme System & ThemeSwitcher", () => {
  let queryClient: QueryClient;
  const mockShowToastError = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    currentMockTheme = "system";

    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
      },
    });

    vi.spyOn(useToastModule, "useToast").mockReturnValue({
      toast: vi.fn(),
      success: vi.fn(),
      error: mockShowToastError,
      dismiss: vi.fn(),
    });
  });

  const renderWithProviders = (ui: React.ReactNode) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <ThemeProvider>{ui}</ThemeProvider>
      </QueryClientProvider>
    );
  };

  it("should default to SYSTEM mode", () => {
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
    } as any);

    renderWithProviders(<ThemeSwitcher variant="segmented" />);

    const systemRadio = screen.getByRole("radio", { name: /Sistem/i });
    expect(systemRadio).toBeDefined();
    expect(systemRadio.getAttribute("aria-checked")).toBe("true");
  });

  it("should render dropdown variant and allow switching theme for unauthenticated user without calling API", async () => {
    const user = userEvent.setup();
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: null,
      isAuthenticated: false,
      isLoading: false,
    } as any);

    renderWithProviders(<ThemeSwitcher variant="dropdown" />);

    const triggerButton = screen.getByRole("button", { name: /Tema tampilan:/i });
    expect(triggerButton).toBeDefined();

    // Open dropdown
    await user.click(triggerButton);

    const darkMenuItem = screen.getByRole("menuitem", { name: /Gelap/i });
    expect(darkMenuItem).toBeDefined();

    // Select DARK
    await user.click(darkMenuItem);

    expect(mockSetTheme).toHaveBeenCalledWith("dark");
    expect(usersApi.updateProfile).not.toHaveBeenCalled();
  });

  it("should synchronize theme changes to the server when user is authenticated", async () => {
    const user = userEvent.setup();
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: {
        id: "user-123",
        username: "testuser",
        email: "test@example.com",
        displayName: "Test User",
        role: "USER",
        isVerified: false,
        themePreference: "SYSTEM",
        createdAt: "2026-09-01",
      },
      isAuthenticated: true,
      isLoading: false,
    } as any);

    vi.mocked(usersApi.updateProfile).mockResolvedValue({
      success: true,
      data: {
        id: "user-123",
        username: "testuser",
        email: "test@example.com",
        displayName: "Test User",
        role: "USER",
        isVerified: false,
        themePreference: "DARK",
        createdAt: "2026-09-01",
      },
    } as any);

    renderWithProviders(<ThemeSwitcher variant="segmented" />);

    const darkRadio = screen.getByRole("radio", { name: /Gelap/i });
    await user.click(darkRadio);

    expect(mockSetTheme).toHaveBeenCalledWith("dark");
    expect(usersApi.updateProfile).toHaveBeenCalledWith({
      themePreference: "DARK",
    });
  });

  it("should handle server synchronization errors gracefully without breaking UI", async () => {
    const user = userEvent.setup();
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: {
        id: "user-123",
        username: "testuser",
        email: "test@example.com",
        displayName: "Test User",
        role: "USER",
        isVerified: false,
        themePreference: "LIGHT",
        createdAt: "2026-09-01",
      },
      isAuthenticated: true,
      isLoading: false,
    } as any);

    vi.mocked(usersApi.updateProfile).mockRejectedValue(new Error("Network connection lost"));

    renderWithProviders(<ThemeSwitcher variant="segmented" />);

    const darkRadio = screen.getByRole("radio", { name: /Gelap/i });
    await user.click(darkRadio);

    expect(mockSetTheme).toHaveBeenCalledWith("dark");
    await waitFor(() => {
      expect(mockShowToastError).toHaveBeenCalledWith(
        expect.stringContaining("Gagal menyimpan preferensi"),
        expect.any(String)
      );
    });
  });

  it("should load authenticated user's server preference automatically on initial load", () => {
    vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: {
        id: "user-123",
        username: "testuser",
        email: "test@example.com",
        displayName: "Test User",
        role: "USER",
        isVerified: false,
        themePreference: "DARK",
        createdAt: "2026-09-01",
      },
      isAuthenticated: true,
      isLoading: false,
    } as any);

    renderWithProviders(<div data-testid="child" />);

    expect(mockSetTheme).toHaveBeenCalledWith("dark");
  });

  it("should protect against stale server responses overwriting newer user selections", async () => {
    const user = userEvent.setup();
    const useAuthSpy = vi.spyOn(useAuthModule, "useAuth").mockReturnValue({
      user: null,
      isAuthenticated: true,
      isLoading: false,
    } as any);

    renderWithProviders(<ThemeSwitcher variant="segmented" />);

    // User explicitly selects LIGHT
    const lightRadio = screen.getByRole("radio", { name: /Terang/i });
    await user.click(lightRadio);

    expect(mockSetTheme).toHaveBeenCalledWith("light");
    mockSetTheme.mockClear();

    // Now an older background response arrives with SYSTEM preference
    act(() => {
      useAuthSpy.mockReturnValue({
        user: {
          id: "user-123",
          username: "testuser",
          themePreference: "SYSTEM",
        },
        isAuthenticated: true,
        isLoading: false,
      } as any);
    });

    // It should NOT call mockSetTheme("system") because the user explicitly chose LIGHT during this session
    expect(mockSetTheme).not.toHaveBeenCalledWith("system");
  });
});
