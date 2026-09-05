import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import React from "react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { ReportModal } from "../components/safety/ReportModal";
import { safetyApi } from "../lib/api/safety";

// Mock the safety API
vi.mock("../lib/api/safety", () => ({
  safetyApi: {
    createReport: vi.fn(),
  },
}));

// Mock useToast hook
const mockSuccess = vi.fn();
const mockError = vi.fn();
vi.mock("../hooks/useToast", () => ({
  useToast: () => ({
    success: mockSuccess,
    error: mockError,
  }),
}));

describe("ReportModal Component", () => {
  const onClose = vi.fn();
  let queryClient: QueryClient;

  beforeEach(() => {
    vi.clearAllMocks();
    queryClient = new QueryClient({
      defaultOptions: { queries: { retry: false } },
    });
  });

  const renderWithProvider = (ui: React.ReactElement) => {
    return render(
      <QueryClientProvider client={queryClient}>
        {ui}
      </QueryClientProvider>
    );
  };

  it("should not render when isOpen is false", () => {
    const { container } = renderWithProvider(
      <ReportModal
        isOpen={false}
        onClose={onClose}
        targetPostId="post-123"
        targetName="johndoe"
      />
    );
    expect(container.firstChild).toBeNull();
  });

  it("should render category options and target info when isOpen is true", () => {
    renderWithProvider(
      <ReportModal
        isOpen={true}
        onClose={onClose}
        targetPostId="post-123"
        targetName="johndoe"
      />
    );

    expect(screen.getByText(/Report Post/i)).toBeDefined();
    expect(screen.getByText(/johndoe/i)).toBeDefined();

    // Check all categories are present
    expect(screen.getByText("Spam")).toBeDefined();
    expect(screen.getByText("Harassment")).toBeDefined();
    expect(screen.getByText("Hate Speech")).toBeDefined();
    expect(screen.getByText("Inappropriate Content")).toBeDefined();
  });

  it("should have submit button disabled until a category is selected", () => {
    renderWithProvider(
      <ReportModal
        isOpen={true}
        onClose={onClose}
        targetPostId="post-123"
        targetName="johndoe"
      />
    );

    const submitBtn = screen.getByRole("button", { name: /Submit Report/i }) as HTMLButtonElement;
    expect(submitBtn.disabled).toBe(true);

    // Select category Spam
    const spamRadio = screen.getByDisplayValue("SPAM");
    fireEvent.click(spamRadio);

    expect(submitBtn.disabled).toBe(false);
  });

  it("should successfully submit report when category is selected and show confirmation", async () => {
    vi.mocked(safetyApi.createReport).mockResolvedValueOnce({
      success: true,
      data: {
        id: "report-999",
        reporterUserId: "user-1",
        targetPostId: "post-123",
        category: "SPAM",
        status: "PENDING",
        createdAt: new Date().toISOString(),
      },
    });

    renderWithProvider(
      <ReportModal
        isOpen={true}
        onClose={onClose}
        targetPostId="post-123"
        targetName="johndoe"
      />
    );

    // Select category Spam
    const spamRadio = screen.getByDisplayValue("SPAM");
    fireEvent.click(spamRadio);

    // Fill optional description
    const textarea = screen.getByPlaceholderText(/Provide any additional context/i);
    fireEvent.change(textarea, { target: { value: "Automated crypto bot scam" } });

    // Submit
    const submitBtn = screen.getByRole("button", { name: /Submit Report/i });
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(safetyApi.createReport).toHaveBeenCalledWith({
        targetPostId: "post-123",
        targetUserId: null,
        category: "SPAM",
        description: "Automated crypto bot scam",
      });
    });

    // Check success view is rendered
    expect(await screen.findByText(/Report Received/i)).toBeDefined();
    expect(mockSuccess).toHaveBeenCalled();
  });

  it("should close modal when clicking the close button", () => {
    renderWithProvider(
      <ReportModal
        isOpen={true}
        onClose={onClose}
        targetPostId="post-123"
        targetName="johndoe"
      />
    );

    const closeBtn = screen.getByLabelText(/Close report modal/i);
    fireEvent.click(closeBtn);

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
