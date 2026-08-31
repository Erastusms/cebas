import { z } from "zod";

export const ALLOWED_IMAGE_TYPES = [
  "image/jpeg",
  "image/png",
  "image/webp",
] as const;

export type AllowedImageMimeType = (typeof ALLOWED_IMAGE_TYPES)[number];

export const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB

export interface ImageValidationResult {
  isValid: boolean;
  error?: string;
}

/**
 * Validates image file type and size for frontend uploads.
 */
export function validateImageFile(file: File | Blob): ImageValidationResult {
  if (!file) {
    return {
      isValid: false,
      error: "No file provided.",
    };
  }

  if (file.size === 0) {
    return {
      isValid: false,
      error: "Selected file is empty.",
    };
  }

  if (file.size > MAX_IMAGE_SIZE_BYTES) {
    return {
      isValid: false,
      error: `File is too large (${(file.size / (1024 * 1024)).toFixed(1)} MB). Maximum allowed size is 5 MB.`,
    };
  }

  const mimeType = file.type?.toLowerCase();
  if (!mimeType || !ALLOWED_IMAGE_TYPES.includes(mimeType as AllowedImageMimeType)) {
    return {
      isValid: false,
      error: `Unsupported image format (${mimeType || "unknown"}). Only JPEG, PNG, and WebP images are allowed.`,
    };
  }

  return { isValid: true };
}

export const createMediaUploadSchema = z.object({
  fileName: z
    .string()
    .min(1, "File name cannot be empty.")
    .max(255, "File name cannot exceed 255 characters."),
  contentType: z
    .string()
    .refine(
      (type) => ALLOWED_IMAGE_TYPES.includes(type.toLowerCase() as AllowedImageMimeType),
      {
        message: "Unsupported MIME type. Allowed types: image/jpeg, image/png, image/webp.",
      }
    ),
  fileSize: z
    .number()
    .min(1, "File size must be greater than 0 bytes.")
    .max(MAX_IMAGE_SIZE_BYTES, "File size cannot exceed 5 MB."),
});

export type CreateMediaUploadFormData = z.infer<typeof createMediaUploadSchema>;
