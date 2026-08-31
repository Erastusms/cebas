export type UploadStatus =
  | "IDLE"
  | "VALIDATING"
  | "PREPARING"
  | "UPLOADING"
  | "CONFIRMING"
  | "SUCCESS"
  | "ERROR";

export type MediaItem = {
  id: string;
  ownerUserId: string;
  originalFileName: string;
  storageKey: string;
  mimeType: string;
  fileSize: number;
  status: string;
  createdAt: string;
  confirmedAt?: string | null;
  url?: string | null;
};

export type CreateMediaUploadRequest = {
  fileName: string;
  contentType: string;
  fileSize: number;
};

export type CreateMediaUploadResponse = {
  mediaId: string;
  uploadUrl: string;
  method: string;
  headers: Record<string, string>;
  expiresAt: string;
};

export type UpdateAvatarRequest = {
  mediaId: string;
};
