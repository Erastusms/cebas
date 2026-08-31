"use client";

import React, { useEffect, useState, useRef } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Camera, Loader2, CheckCircle2, AlertCircle } from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { editProfileSchema, type EditProfileFormData } from "../../lib/validations/auth";
import { validateImageFile } from "../../lib/validations/media";
import { useProfile } from "../../hooks/useProfile";
import { useDirectUpload } from "../../hooks/useDirectUpload";
import { mediaApi } from "../../lib/api/media";
import { useToast } from "../../hooks/useToast";
import { Modal } from "../ui/modal";
import { Input } from "../ui/input";
import { Button } from "../ui/button";
import { AvatarCropModal } from "../media/AvatarCropModal";
import type { User } from "../../types/auth";
import { ProblemDetailsException } from "../../lib/api/errors";

interface EditProfileModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentUser: User;
}

export function EditProfileModal({
  isOpen,
  onClose,
  currentUser,
}: EditProfileModalProps) {
  const queryClient = useQueryClient();
  const { updateProfile, isUpdating } = useProfile();
  const { success: showToastSuccess, error: showToastError } = useToast();
  const { upload, progress, status, isUploading, reset: resetUpload, error: uploadError } = useDirectUpload();

  const [serverError, setServerError] = useState<string | null>(null);
  const [selectedFileForCrop, setSelectedFileForCrop] = useState<File | null>(null);
  const [isCropModalOpen, setIsCropModalOpen] = useState(false);
  const [currentAvatarUrl, setCurrentAvatarUrl] = useState<string | null>(currentUser.avatarUrl || null);

  const fileInputRef = useRef<HTMLInputElement | null>(null);

  const {
    register,
    handleSubmit,
    watch,
    reset,
    formState: { errors },
  } = useForm<EditProfileFormData>({
    resolver: zodResolver(editProfileSchema),
    defaultValues: {
      displayName: currentUser.displayName || "",
      bio: currentUser.bio || "",
    },
  });

  useEffect(() => {
    if (isOpen) {
      reset({
        displayName: currentUser.displayName || "",
        bio: currentUser.bio || "",
      });
      setCurrentAvatarUrl(currentUser.avatarUrl || null);
      setServerError(null);
      resetUpload();
    }
  }, [isOpen, currentUser, reset, resetUpload]);

  const watchedDisplayName = watch("displayName") || "";
  const watchedBio = watch("bio") || "";

  // Handle local file selection from input
  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    // Reset input value so same file can be re-selected if desired
    e.target.value = "";

    const validation = validateImageFile(file);
    if (!validation.isValid) {
      setServerError(validation.error || "Invalid image file selected.");
      return;
    }

    setServerError(null);
    setSelectedFileForCrop(file);
    setIsCropModalOpen(true);
  };

  // Handle crop confirmation & direct upload sequence
  const handleConfirmCrop = async (croppedBlob: Blob) => {
    setIsCropModalOpen(false);
    setServerError(null);

    try {
      // 1. Upload cropped binary directly to storage and confirm READY state
      const media = await upload(croppedBlob, "avatar.webp");

      // 2. Assign media as user avatar
      const userRes = await mediaApi.updateAvatar(media.id);

      // 3. Update query cache and local state
      const updatedUser = userRes.data;
      setCurrentAvatarUrl(updatedUser.avatarUrl || `/api/v1/media/${media.id}`);
      queryClient.setQueryData(["currentUser"], updatedUser);
      queryClient.invalidateQueries({ queryKey: ["currentUser"] });
      queryClient.invalidateQueries({
        queryKey: ["profile", updatedUser.username.toLowerCase()],
      });

      showToastSuccess("Avatar photo updated successfully.", "Avatar Updated");
    } catch (err: unknown) {
      if (err instanceof Error) {
        setServerError(err.message);
      } else {
        setServerError("Failed to upload avatar photo.");
      }
      showToastError("Could not upload avatar. Please try again.", "Upload Error");
    }
  };

  const onSubmit = async (data: EditProfileFormData) => {
    setServerError(null);
    try {
      await updateProfile({
        displayName: data.displayName,
        bio: data.bio || null,
      });
      onClose();
    } catch (err: unknown) {
      if (err instanceof ProblemDetailsException) {
        setServerError(
          err.problem.detail ||
            err.problem.title ||
            "Failed to update profile."
        );
      } else if (err instanceof Error) {
        setServerError(err.message);
      } else {
        setServerError("An unexpected error occurred.");
      }
    }
  };

  const isAnyProcessing = isUpdating || isUploading;

  return (
    <>
      <Modal
        isOpen={isOpen}
        onClose={onClose}
        title="Edit Profile"
        description="Update your public profile, avatar, and biography."
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-5">
          {/* Global Error Banner */}
          {(serverError || uploadError) && (
            <div
              role="alert"
              className="flex items-center space-x-2 p-3 text-xs rounded-lg border border-destructive/50 bg-destructive/10 text-destructive font-medium"
            >
              <AlertCircle className="h-4 w-4 shrink-0" />
              <span>{serverError || uploadError}</span>
            </div>
          )}

          {/* Avatar Uploader Section */}
          <div className="flex flex-col sm:flex-row items-center sm:items-start space-y-3 sm:space-y-0 sm:space-x-4 p-3 rounded-xl border border-border bg-muted/20">
            <div className="relative group">
              <div className="flex h-20 w-20 items-center justify-center rounded-full border-2 border-border bg-primary text-2xl font-bold text-primary-foreground shadow-sm overflow-hidden">
                {currentAvatarUrl ? (
                  <img
                    src={currentAvatarUrl}
                    alt={currentUser.displayName || currentUser.username}
                    className="h-full w-full object-cover"
                    onError={() => setCurrentAvatarUrl(null)}
                  />
                ) : (
                  currentUser.displayName?.charAt(0).toUpperCase() ||
                  currentUser.username.charAt(0).toUpperCase()
                )}
              </div>

              {/* Camera Action Overlay */}
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                disabled={isAnyProcessing}
                className="absolute inset-0 flex items-center justify-center rounded-full bg-black/50 text-white opacity-0 group-hover:opacity-100 transition-opacity disabled:pointer-events-none"
                title="Change Avatar"
              >
                <Camera className="h-6 w-6" />
              </button>
            </div>

            <div className="flex-1 text-center sm:text-left space-y-1.5">
              <div className="flex flex-wrap items-center gap-2 justify-center sm:justify-start">
                <input
                  type="file"
                  ref={fileInputRef}
                  onChange={handleFileSelect}
                  accept="image/jpeg,image/png,image/webp"
                  className="hidden"
                />
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={isAnyProcessing}
                  className="text-xs"
                >
                  <Camera className="h-3.5 w-3.5 mr-1.5" />
                  Change Avatar
                </Button>
                {isUploading && (
                  <span className="inline-flex items-center text-xs text-primary font-medium">
                    <Loader2 className="h-3.5 w-3.5 mr-1 animate-spin" />
                    {status === "PREPARING" && "Preparing upload..."}
                    {status === "UPLOADING" && `Uploading ${progress}%`}
                    {status === "CONFIRMING" && "Confirming..."}
                  </span>
                )}
                {status === "SUCCESS" && (
                  <span className="inline-flex items-center text-xs text-emerald-500 font-medium">
                    <CheckCircle2 className="h-3.5 w-3.5 mr-1" />
                    Uploaded
                  </span>
                )}
              </div>
              <p className="text-[11px] text-muted-foreground">
                JPEG, PNG, or WebP. Maximum size 5 MB. Square aspect ratio recommended.
              </p>

              {/* Live Progress Bar */}
              {isUploading && (
                <div className="w-full bg-muted rounded-full h-1.5 mt-2 overflow-hidden">
                  <div
                    className="bg-primary h-1.5 rounded-full transition-all duration-200"
                    style={{ width: `${progress}%` }}
                  />
                </div>
              )}
            </div>
          </div>

          {/* Display Name Input */}
          <div className="space-y-1">
            <div className="flex justify-between items-center text-xs">
              <span className="font-medium text-foreground">Display Name</span>
              <span
                className={
                  watchedDisplayName.length > 50
                    ? "text-destructive font-medium"
                    : "text-muted-foreground"
                }
              >
                {watchedDisplayName.length}/50
              </span>
            </div>
            <Input
              id="edit-display-name"
              placeholder="Your name"
              {...register("displayName")}
              error={errors.displayName?.message}
            />
          </div>

          {/* Biography Textarea */}
          <div className="space-y-1">
            <div className="flex justify-between items-center text-xs">
              <span className="font-medium text-foreground">Biography</span>
              <span
                className={
                  watchedBio.length > 160
                    ? "text-destructive font-medium"
                    : "text-muted-foreground"
                }
              >
                {watchedBio.length}/160
              </span>
            </div>
            <textarea
              id="edit-bio"
              rows={3}
              placeholder="Tell the world about yourself..."
              className="flex w-full rounded-lg border border-border bg-background px-3 py-2 text-sm text-foreground ring-offset-background placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 resize-none"
              {...register("bio")}
            />
            {errors.bio && (
              <p className="text-xs font-medium text-destructive">
                {errors.bio.message}
              </p>
            )}
          </div>

          {/* Action Buttons */}
          <div className="flex justify-end space-x-2 pt-3 border-t border-border">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onClose}
              disabled={isAnyProcessing}
            >
              Cancel
            </Button>
            <Button
              type="submit"
              variant="default"
              size="sm"
              isLoading={isUpdating}
              disabled={isAnyProcessing}
            >
              Save Changes
            </Button>
          </div>
        </form>
      </Modal>

      {/* Interactive Avatar Cropper Modal */}
      {selectedFileForCrop && (
        <AvatarCropModal
          isOpen={isCropModalOpen}
          onClose={() => {
            setIsCropModalOpen(false);
            setSelectedFileForCrop(null);
          }}
          imageFile={selectedFileForCrop}
          onConfirmCrop={handleConfirmCrop}
          isProcessing={isUploading}
        />
      )}
    </>
  );
}
