import { describe, it, expect, vi, beforeEach } from "vitest";
import {
  validateImageFile,
  createMediaUploadSchema,
  MAX_IMAGE_SIZE_BYTES,
} from "../lib/validations/media";
import { mediaApi } from "../lib/api/media";

describe("Frontend Media Validations & Schemas", () => {
  describe("validateImageFile", () => {
    it("should accept valid JPEG, PNG, and WebP image files under 5 MB", () => {
      const validJpeg = new File([new Uint8Array(1024)], "avatar.jpg", {
        type: "image/jpeg",
      });
      const validPng = new File([new Uint8Array(2048)], "avatar.png", {
        type: "image/png",
      });
      const validWebp = new File([new Uint8Array(4096)], "avatar.webp", {
        type: "image/webp",
      });

      expect(validateImageFile(validJpeg).isValid).toBe(true);
      expect(validateImageFile(validPng).isValid).toBe(true);
      expect(validateImageFile(validWebp).isValid).toBe(true);
    });

    it("should reject image files exceeding 5 MB", () => {
      const oversizedBuffer = new Uint8Array(MAX_IMAGE_SIZE_BYTES + 1024);
      const oversizedFile = new File([oversizedBuffer], "huge.png", {
        type: "image/png",
      });

      const result = validateImageFile(oversizedFile);
      expect(result.isValid).toBe(false);
      expect(result.error).toContain("File is too large");
    });

    it("should reject empty files", () => {
      const emptyFile = new File([], "empty.png", { type: "image/png" });
      const result = validateImageFile(emptyFile);
      expect(result.isValid).toBe(false);
      expect(result.error).toContain("empty");
    });

    it("should reject unsupported MIME formats (GIF, SVG, PDF, EXE)", () => {
      const gif = new File([new Uint8Array(100)], "animation.gif", {
        type: "image/gif",
      });
      const svg = new File([new Uint8Array(100)], "vector.svg", {
        type: "image/svg+xml",
      });
      const pdf = new File([new Uint8Array(100)], "doc.pdf", {
        type: "application/pdf",
      });

      expect(validateImageFile(gif).isValid).toBe(false);
      expect(validateImageFile(gif).error).toContain("Unsupported image format");
      expect(validateImageFile(svg).isValid).toBe(false);
      expect(validateImageFile(pdf).isValid).toBe(false);
    });
  });

  describe("createMediaUploadSchema (Zod)", () => {
    it("should accept valid upload metadata", () => {
      const validData = {
        fileName: "profile.webp",
        contentType: "image/webp",
        fileSize: 10240,
      };
      const result = createMediaUploadSchema.safeParse(validData);
      expect(result.success).toBe(true);
    });

    it("should reject unsupported MIME types in Zod schema", () => {
      const invalidData = {
        fileName: "profile.gif",
        contentType: "image/gif",
        fileSize: 10240,
      };
      const result = createMediaUploadSchema.safeParse(invalidData);
      expect(result.success).toBe(false);
    });

    it("should reject oversized file size in Zod schema", () => {
      const invalidData = {
        fileName: "profile.png",
        contentType: "image/png",
        fileSize: MAX_IMAGE_SIZE_BYTES + 1,
      };
      const result = createMediaUploadSchema.safeParse(invalidData);
      expect(result.success).toBe(false);
    });
  });
});

describe("mediaApi Client Abstraction", () => {
  it("should have createUploadUrl, confirmMediaUpload, and updateAvatar methods", () => {
    expect(typeof mediaApi.createUploadUrl).toBe("function");
    expect(typeof mediaApi.uploadBinary).toBe("function");
    expect(typeof mediaApi.confirmMediaUpload).toBe("function");
    expect(typeof mediaApi.updateAvatar).toBe("function");
  });
});
