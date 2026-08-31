"use client";

import { useState, useCallback } from "react";
import { mediaApi } from "../lib/api/media";
import { validateImageFile } from "../lib/validations/media";
import type { MediaItem, UploadStatus } from "../types/media";
import { ProblemDetailsException, ApiException } from "../lib/api/errors";

export interface UseDirectUploadReturn {
  upload: (file: File | Blob, customFileName?: string) => Promise<MediaItem>;
  progress: number;
  status: UploadStatus;
  media: MediaItem | null;
  error: string | null;
  isUploading: boolean;
  reset: () => void;
}

export function useDirectUpload(): UseDirectUploadReturn {
  const [status, setStatus] = useState<UploadStatus>("IDLE");
  const [progress, setProgress] = useState<number>(0);
  const [media, setMedia] = useState<MediaItem | null>(null);
  const [error, setError] = useState<string | null>(null);

  const reset = useCallback(() => {
    setStatus("IDLE");
    setProgress(0);
    setMedia(null);
    setError(null);
  }, []);

  const upload = useCallback(
    async (file: File | Blob, customFileName?: string): Promise<MediaItem> => {
      setError(null);
      setProgress(0);

      try {
        // 1. Validation phase
        setStatus("VALIDATING");
        const validation = validateImageFile(file);
        if (!validation.isValid) {
          throw new Error(validation.error || "File validation failed.");
        }

        const fileName =
          customFileName ||
          (file instanceof File ? file.name : "upload.webp");
        const contentType = file.type || "image/webp";
        const fileSize = file.size;

        // 2. Preparation phase (request upload URL & pre-create media in UPLOADING state)
        setStatus("PREPARING");
        const prepRes = await mediaApi.createUploadUrl({
          fileName,
          contentType,
          fileSize,
        });

        const { mediaId, uploadUrl } = prepRes.data;

        // 3. Direct binary transfer phase
        setStatus("UPLOADING");
        await mediaApi.uploadBinary(uploadUrl, file, contentType, (pct) => {
          setProgress(pct);
        });

        // 4. Confirmation phase
        setStatus("CONFIRMING");
        setProgress(100);
        const confirmRes = await mediaApi.confirmMediaUpload(mediaId);

        // 5. Success
        setStatus("SUCCESS");
        setMedia(confirmRes.data);
        return confirmRes.data;
      } catch (err: unknown) {
        setStatus("ERROR");
        let errorMessage = "Media upload failed.";

        if (err instanceof ProblemDetailsException) {
          errorMessage =
            err.problem.detail ||
            err.problem.title ||
            (err.problem.errors
              ? Object.values(err.problem.errors).flat().join(" ")
              : "Media upload rejected.");
        } else if (err instanceof ApiException) {
          errorMessage = err.message;
        } else if (err instanceof Error) {
          errorMessage = err.message;
        }

        setError(errorMessage);
        throw new Error(errorMessage);
      }
    },
    []
  );

  const isUploading =
    status === "VALIDATING" ||
    status === "PREPARING" ||
    status === "UPLOADING" ||
    status === "CONFIRMING";

  return {
    upload,
    progress,
    status,
    media,
    error,
    isUploading,
    reset,
  };
}
