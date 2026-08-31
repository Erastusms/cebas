"use client";

import React, { useState, useRef } from "react";
import { Image as ImageIcon, X, Send, Loader2, AlertCircle } from "lucide-react";
import { Button } from "../ui/button";
import { mediaApi } from "../../lib/api/media";
import { postsApi } from "../../lib/api/posts";
import { useToast } from "../../hooks/useToast";
import type { Post } from "../../types/api";

interface PostComposerProps {
  onPostCreated?: (post: Post) => void;
  placeholder?: string;
  className?: string;
}

type UploadingMedia = {
  id?: string;
  file: File;
  previewUrl: string;
  progress: number;
  status: "UPLOADING" | "READY" | "ERROR";
  error?: string;
};

export function PostComposer({
  onPostCreated,
  placeholder = "What's happening?",
  className = "",
}: PostComposerProps) {
  const { success, error: toastError } = useToast();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [content, setContent] = useState("");
  const [mediaList, setMediaList] = useState<UploadingMedia[]>([]);
  const [isPublishing, setIsPublishing] = useState(false);
  const [composerError, setComposerError] = useState<string | null>(null);

  const maxChars = 1000;
  const charsRemaining = maxChars - content.length;
  const isOverLimit = charsRemaining < 0;

  const isUploadingAny = mediaList.some((m) => m.status === "UPLOADING");
  const readyMediaIds = mediaList.filter((m) => m.status === "READY" && m.id).map((m) => m.id!);

  const canSubmit =
    !isPublishing &&
    !isUploadingAny &&
    !isOverLimit &&
    (content.trim().length > 0 || readyMediaIds.length > 0);

  const handleFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = e.target.files;
    if (!files || files.length === 0) return;

    setComposerError(null);
    const availableSlots = 4 - mediaList.length;
    if (availableSlots <= 0) {
      toastError("Maximum of 4 images allowed per post.");
      return;
    }

    const selectedFiles = Array.from(files).slice(0, availableSlots);
    const allowedTypes = ["image/jpeg", "image/png", "image/webp"];

    for (const file of selectedFiles) {
      if (!allowedTypes.includes(file.type)) {
        toastError(`Unsupported file format '${file.name}'. Only JPEG, PNG, and WebP are supported.`);
        continue;
      }

      if (file.size > 5 * 1024 * 1024) {
        toastError(`File '${file.name}' exceeds the 5 MB limit.`);
        continue;
      }

      const previewUrl = URL.createObjectURL(file);
      const newMedia: UploadingMedia = {
        file,
        previewUrl,
        progress: 0,
        status: "UPLOADING",
      };

      setMediaList((prev) => [...prev, newMedia]);
      uploadFile(newMedia);
    }

    // Reset file input
    if (fileInputRef.current) {
      fileInputRef.current.value = "";
    }
  };

  const uploadFile = async (uploadingItem: UploadingMedia) => {
    try {
      // 1. Request upload URL
      const prepRes = await mediaApi.createUploadUrl({
        fileName: uploadingItem.file.name,
        contentType: uploadingItem.file.type,
        fileSize: uploadingItem.file.size,
      });

      const { mediaId, uploadUrl } = prepRes.data;

      // 2. Binary upload with progress
      await mediaApi.uploadBinary(
        uploadUrl,
        uploadingItem.file,
        uploadingItem.file.type,
        (percent) => {
          setMediaList((prev) =>
            prev.map((item) =>
              item.previewUrl === uploadingItem.previewUrl
                ? { ...item, progress: percent }
                : item
            )
          );
        }
      );

      // 3. Confirm upload
      await mediaApi.confirmMediaUpload(mediaId);

      setMediaList((prev) =>
        prev.map((item) =>
          item.previewUrl === uploadingItem.previewUrl
            ? { ...item, id: mediaId, status: "READY", progress: 100 }
            : item
        )
      );
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to upload image.";
      setMediaList((prev) =>
        prev.map((item) =>
          item.previewUrl === uploadingItem.previewUrl
            ? { ...item, status: "ERROR", error: msg }
            : item
        )
      );
      toastError(`Failed to upload ${uploadingItem.file.name}`);
    }
  };

  const handleRemoveMedia = (previewUrl: string) => {
    setMediaList((prev) => {
      const target = prev.find((m) => m.previewUrl === previewUrl);
      if (target?.previewUrl) {
        URL.revokeObjectURL(target.previewUrl);
      }
      return prev.filter((m) => m.previewUrl !== previewUrl);
    });
  };

  const handlePublish = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;

    setIsPublishing(true);
    setComposerError(null);

    try {
      const response = await postsApi.createPost({
        content: content.trim(),
        mediaIds: readyMediaIds.length > 0 ? readyMediaIds : undefined,
      });

      success("Post published successfully!");
      setContent("");
      setMediaList([]);
      if (onPostCreated) {
        onPostCreated(response.data);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to publish post.";
      setComposerError(msg);
      toastError(msg);
    } finally {
      setIsPublishing(false);
    }
  };

  return (
    <div className={`rounded-2xl border border-border bg-card p-4 sm:p-6 shadow-sm ${className}`}>
      <form onSubmit={handlePublish} className="space-y-4">
        {/* Text Area */}
        <div className="relative">
          <textarea
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder={placeholder}
            rows={3}
            disabled={isPublishing}
            maxLength={1100}
            style={{ outline: "none", border: "none", boxShadow: "none" }}
            className="w-full resize-none border-none bg-transparent p-0 text-sm sm:text-base text-foreground placeholder:text-muted-foreground outline-none focus:outline-none focus:ring-0 focus-visible:ring-0 focus-visible:outline-none shadow-none"
            aria-label="Post text content"
          />
        </div>

        {/* Media Preview Grid */}
        {mediaList.length > 0 && (
          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4 pt-1">
            {mediaList.map((item, idx) => (
              <div
                key={item.previewUrl || idx}
                className="group relative aspect-square overflow-hidden rounded-xl border border-border bg-muted/30"
              >
                <img
                  src={item.previewUrl}
                  alt={item.file.name}
                  className={`h-full w-full object-cover transition ${
                    item.status === "UPLOADING" ? "opacity-60" : "opacity-100"
                  }`}
                />

                {item.status === "UPLOADING" && (
                  <div className="absolute inset-0 flex flex-col items-center justify-center bg-black/40 p-2 text-white">
                    <Loader2 className="h-5 w-5 animate-spin mb-1" />
                    <span className="text-[10px] font-semibold">{item.progress}%</span>
                  </div>
                )}

                {item.status === "ERROR" && (
                  <div className="absolute inset-0 flex flex-col items-center justify-center bg-destructive/60 p-2 text-white text-center">
                    <AlertCircle className="h-5 w-5 mb-1" />
                    <span className="text-[10px] line-clamp-2">Upload failed</span>
                  </div>
                )}

                <button
                  type="button"
                  onClick={() => handleRemoveMedia(item.previewUrl)}
                  disabled={isPublishing}
                  className="absolute right-1.5 top-1.5 rounded-full bg-black/60 p-1 text-white opacity-90 transition hover:bg-black/80 hover:opacity-100 focus:outline-none focus:ring-1 focus:ring-primary"
                  aria-label={`Remove image ${idx + 1}`}
                >
                  <X className="h-3.5 w-3.5" />
                </button>
              </div>
            ))}
          </div>
        )}

        {/* Error notification */}
        {composerError && (
          <div className="flex items-center space-x-2 rounded-lg bg-destructive/10 p-3 text-xs text-destructive">
            <AlertCircle className="h-4 w-4 flex-shrink-0" />
            <span>{composerError}</span>
          </div>
        )}

        {/* Toolbar & Footer */}
        <div className="flex items-center justify-between border-t border-border pt-3">
          <div className="flex items-center space-x-2">
            <input
              type="file"
              ref={fileInputRef}
              onChange={handleFileSelect}
              multiple
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
              disabled={isPublishing || mediaList.length >= 4}
            />

            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={() => fileInputRef.current?.click()}
              disabled={isPublishing || mediaList.length >= 4}
              aria-label="Attach images"
              className="text-xs text-muted-foreground hover:text-foreground"
            >
              <ImageIcon className="mr-1.5 h-4 w-4 text-primary" />
              <span>Attach Image ({mediaList.length}/4)</span>
            </Button>
          </div>

          <div className="flex items-center space-x-3">
            {/* Dynamic Character Counter */}
            <span
              className={`text-xs font-mono font-medium ${
                isOverLimit
                  ? "text-destructive font-bold"
                  : charsRemaining <= 50
                    ? "text-amber-500 font-semibold"
                    : "text-muted-foreground"
              }`}
              aria-live="polite"
            >
              {content.length} / {maxChars}
            </span>

            <Button
              type="submit"
              variant="default"
              size="sm"
              disabled={!canSubmit}
              isLoading={isPublishing}
              className="font-medium"
            >
              <Send className="mr-1.5 h-3.5 w-3.5" />
              <span>Post</span>
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
}
