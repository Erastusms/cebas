import { describe, it, expect, vi, beforeEach } from "vitest";
import { renderHook, act } from "@testing-library/react";
import { useDirectUpload } from "../hooks/useDirectUpload";
import { mediaApi } from "../lib/api/media";

vi.mock("../lib/api/media", () => ({
  mediaApi: {
    createUploadUrl: vi.fn(),
    uploadBinary: vi.fn(),
    confirmMediaUpload: vi.fn(),
    updateAvatar: vi.fn(),
  },
}));

describe("useDirectUpload Hook", () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it("should initialize with IDLE state and zero progress", () => {
    const { result } = renderHook(() => useDirectUpload());

    expect(result.current.status).toBe("IDLE");
    expect(result.current.progress).toBe(0);
    expect(result.current.media).toBeNull();
    expect(result.current.error).toBeNull();
    expect(result.current.isUploading).toBe(false);
  });

  it("should transition through upload phases and return confirmed media", async () => {
    const mockMediaId = "018f0000-0000-7000-8000-000000000001";
    const mockUploadUrl = "/api/v1/media/upload?key=media/user/1.webp";
    const mockMediaItem = {
      id: mockMediaId,
      ownerUserId: "018f0000-0000-7000-8000-000000000002",
      originalFileName: "avatar.webp",
      storageKey: "media/user/1.webp",
      mimeType: "image/webp",
      fileSize: 1024,
      status: "READY",
      createdAt: new Date().toISOString(),
      confirmedAt: new Date().toISOString(),
    };

    vi.mocked(mediaApi.createUploadUrl).mockResolvedValue({
      success: true,
      data: {
        mediaId: mockMediaId,
        uploadUrl: mockUploadUrl,
        method: "PUT",
        headers: { "Content-Type": "image/webp" },
        expiresAt: new Date().toISOString(),
      },
    });

    vi.mocked(mediaApi.uploadBinary).mockImplementation(
      async (url, file, ct, onProgress) => {
        if (onProgress) onProgress(50);
        if (onProgress) onProgress(100);
      }
    );

    vi.mocked(mediaApi.confirmMediaUpload).mockResolvedValue({
      success: true,
      data: mockMediaItem,
    });

    const { result } = renderHook(() => useDirectUpload());
    const validFile = new File([new Uint8Array(1024)], "avatar.webp", {
      type: "image/webp",
    });

    let uploadedMedia;
    await act(async () => {
      uploadedMedia = await result.current.upload(validFile);
    });

    expect(mediaApi.createUploadUrl).toHaveBeenCalledTimes(1);
    expect(mediaApi.uploadBinary).toHaveBeenCalledTimes(1);
    expect(mediaApi.confirmMediaUpload).toHaveBeenCalledWith(mockMediaId);

    expect(result.current.status).toBe("SUCCESS");
    expect(result.current.media).toEqual(mockMediaItem);
    expect(result.current.progress).toBe(100);
    expect(result.current.error).toBeNull();
  });

  it("should set ERROR state and throw when file is invalid", async () => {
    const { result } = renderHook(() => useDirectUpload());
    const invalidFile = new File([new Uint8Array(100)], "doc.pdf", {
      type: "application/pdf",
    });

    let thrownError: unknown;
    await act(async () => {
      try {
        await result.current.upload(invalidFile);
      } catch (err) {
        thrownError = err;
      }
    });

    expect(thrownError).toBeDefined();
    expect(result.current.status).toBe("ERROR");
    expect(result.current.error).toContain("Unsupported image format");
    expect(mediaApi.createUploadUrl).not.toHaveBeenCalled();
  });

  it("should reset state back to IDLE when reset is called", () => {
    const { result } = renderHook(() => useDirectUpload());

    act(() => {
      result.current.reset();
    });

    expect(result.current.status).toBe("IDLE");
    expect(result.current.progress).toBe(0);
    expect(result.current.error).toBeNull();
  });
});
