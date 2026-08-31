import { apiClient } from "./client";
import type { ApiResponse } from "./types";
import type {
  MediaItem,
  CreateMediaUploadRequest,
  CreateMediaUploadResponse,
  UpdateAvatarRequest,
} from "../../types/api";
import type { User } from "../../types/auth";

export const mediaApi = {
  /**
   * Requests a pre-signed direct upload target URL.
   */
  createUploadUrl: async (
    data: CreateMediaUploadRequest
  ): Promise<ApiResponse<CreateMediaUploadResponse>> => {
    return apiClient.post<ApiResponse<CreateMediaUploadResponse>>(
      "/api/v1/media/upload-url",
      data
    );
  },

  /**
   * Uploads raw binary file directly to storage target reporting progress events.
   */
  uploadBinary: (
    uploadUrl: string,
    file: Blob | File,
    contentType: string,
    onProgress?: (percent: number) => void
  ): Promise<void> => {
    return new Promise((resolve, reject) => {
      const xhr = new XMLHttpRequest();

      // Resolve full target URL if relative
      let targetUrl = uploadUrl;
      if (uploadUrl.startsWith("/")) {
        const baseUrl = (
          process.env.NEXT_PUBLIC_API_BASE_URL ??
          (typeof window !== "undefined" ? window.location.origin : "http://localhost:5226")
        ).replace(/\/$/, "");
        targetUrl = `${baseUrl}${uploadUrl}`;
      }

      xhr.open("PUT", targetUrl, true);
      xhr.setRequestHeader("Content-Type", contentType);
      xhr.withCredentials = true;

      if (xhr.upload && onProgress) {
        xhr.upload.onprogress = (event) => {
          if (event.lengthComputable) {
            const percent = Math.round((event.loaded / event.total) * 100);
            onProgress(Math.min(100, Math.max(0, percent)));
          }
        };
      }

      xhr.onload = () => {
        if (xhr.status >= 200 && xhr.status < 300) {
          if (onProgress) onProgress(100);
          resolve();
        } else {
          try {
            const errJson = JSON.parse(xhr.responseText);
            reject(
              new Error(
                errJson.detail ||
                  errJson.title ||
                  errJson.message ||
                  `Upload failed with status ${xhr.status}`
              )
            );
          } catch {
            reject(new Error(`Upload failed with status ${xhr.status}`));
          }
        }
      };

      xhr.onerror = () => {
        reject(new Error("Network error during file binary upload."));
      };

      xhr.ontimeout = () => {
        reject(new Error("Upload timed out. Please try again."));
      };

      xhr.send(file);
    });
  },

  /**
   * Confirms upload completion in backend to transition media to READY state.
   */
  confirmMediaUpload: async (
    mediaId: string
  ): Promise<ApiResponse<MediaItem>> => {
    return apiClient.post<ApiResponse<MediaItem>>(
      `/api/v1/media/${encodeURIComponent(mediaId)}/confirm`
    );
  },

  /**
   * Updates authenticated user's avatar with confirmed media ID.
   */
  updateAvatar: async (
    mediaId: string
  ): Promise<ApiResponse<User>> => {
    const payload: UpdateAvatarRequest = { mediaId };
    return apiClient.put<ApiResponse<User>>("/api/v1/users/me/avatar", payload);
  },
};
