"use client";

import React, { useEffect, useState, useRef } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { Camera, Loader2, Check, AlertCircle, X, Sparkles } from "lucide-react";
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
import { BANNER_GRADIENTS, resolveBannerStyle } from "../../lib/utils/gradients";
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
  const { upload, isUploading, reset: resetUpload, error: uploadError } = useDirectUpload();
  const {
    upload: uploadBanner,
    progress: bannerProgress,
    isUploading: isBannerUploading,
    reset: resetBannerUpload,
    error: bannerUploadError,
  } = useDirectUpload();

  const [serverError, setServerError] = useState<string | null>(null);
  const [selectedFileForCrop, setSelectedFileForCrop] = useState<File | null>(null);
  const [isCropModalOpen, setIsCropModalOpen] = useState(false);
  const [currentAvatarUrl, setCurrentAvatarUrl] = useState<string | null>(currentUser.avatarUrl || null);
  const [currentBannerUrl, setCurrentBannerUrl] = useState<string | null>(currentUser.bannerUrl || null);

  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const bannerInputRef = useRef<HTMLInputElement | null>(null);

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
      setCurrentBannerUrl(currentUser.bannerUrl || null);
      setServerError(null);
      resetUpload();
      resetBannerUpload();
    }
  }, [isOpen, currentUser, reset, resetUpload, resetBannerUpload]);

  const watchedDisplayName = watch("displayName") || "";
  const watchedBio = watch("bio") || "";

  // Handle avatar file selection
  const handleFileSelect = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

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

  // Handle avatar crop confirmation & upload
  const handleConfirmCrop = async (croppedBlob: Blob) => {
    setIsCropModalOpen(false);
    setServerError(null);

    try {
      const media = await upload(croppedBlob, "avatar.webp");
      const userRes = await mediaApi.updateAvatar(media.id);

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

  // Handle banner file selection & upload
  const handleBannerFileSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    e.target.value = "";

    const validation = validateImageFile(file);
    if (!validation.isValid) {
      setServerError(validation.error || "Invalid banner image selected.");
      return;
    }

    setServerError(null);

    try {
      const media = await uploadBanner(file, file.name);
      const bannerUrl = `/api/v1/media/${media.id}`;
      setCurrentBannerUrl(bannerUrl);
      showToastSuccess("Banner uploaded. Click 'Save Changes' to apply.", "Banner Ready");
    } catch (err: unknown) {
      if (err instanceof Error) {
        setServerError(err.message);
      } else {
        setServerError("Failed to upload banner image.");
      }
      showToastError("Could not upload banner. Please try again.", "Upload Error");
    }
  };

  const onSubmit = async (data: EditProfileFormData) => {
    setServerError(null);
    try {
      await updateProfile({
        displayName: data.displayName,
        bio: data.bio || null,
        bannerUrl: currentBannerUrl,
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

  const isAnyProcessing = isUpdating || isUploading || isBannerUploading;

  return (
    <>
      <Modal
        isOpen={isOpen}
        onClose={onClose}
        title="Edit Profile"
        className="max-w-lg p-0 overflow-hidden"
      >
        <form onSubmit={handleSubmit(onSubmit)}>
          {/* Global Error Banner */}
          {(serverError || uploadError || bannerUploadError) && (
            <div className="px-6 pt-4">
              <div
                role="alert"
                className="flex items-center space-x-2 p-3 text-xs rounded-lg border border-destructive/50 bg-destructive/10 text-destructive font-medium"
              >
                <AlertCircle className="h-4 w-4 shrink-0" />
                <span>{serverError || uploadError || bannerUploadError}</span>
              </div>
            </div>
          )}

          {/* Unified Twitter-Style Header & Avatar Preview */}
          <div className="relative">
            {/* Banner Cover Area */}
            <div
              className="w-full h-32 sm:h-36 relative overflow-hidden transition-all bg-muted flex items-center justify-center group"
              style={resolveBannerStyle(currentBannerUrl, currentUser.username)}
            >
              {/* Banner Action Buttons */}
              <div className="flex items-center space-x-2 z-10">
                <button
                  type="button"
                  onClick={() => bannerInputRef.current?.click()}
                  disabled={isAnyProcessing}
                  className="inline-flex items-center space-x-1.5 bg-black/60 hover:bg-black/80 text-white text-xs font-medium px-3 py-1.5 rounded-full backdrop-blur-sm shadow-md transition disabled:opacity-50 cursor-pointer"
                  title="Upload Banner Image"
                >
                  <Camera className="h-3.5 w-3.5" />
                  <span>Upload Header</span>
                </button>

                {currentBannerUrl && (
                  <button
                    type="button"
                    onClick={() => setCurrentBannerUrl(null)}
                    disabled={isAnyProcessing}
                    className="inline-flex items-center justify-center h-7 w-7 rounded-full bg-black/60 hover:bg-black/80 text-white backdrop-blur-sm shadow-md transition cursor-pointer"
                    title="Reset to default gradient"
                  >
                    <X className="h-3.5 w-3.5" />
                  </button>
                )}
              </div>

              {/* Banner Upload Progress Bar */}
              {isBannerUploading && (
                <div className="absolute bottom-0 inset-x-0 bg-black/40 p-1 backdrop-blur-xs">
                  <div className="w-full bg-white/20 rounded-full h-1 overflow-hidden">
                    <div
                      className="bg-primary h-1 rounded-full transition-all duration-200"
                      style={{ width: `${bannerProgress}%` }}
                    />
                  </div>
                </div>
              )}
            </div>

            {/* Hidden File Inputs */}
            <input
              type="file"
              ref={bannerInputRef}
              onChange={handleBannerFileSelect}
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
            />
            <input
              type="file"
              ref={fileInputRef}
              onChange={handleFileSelect}
              accept="image/jpeg,image/png,image/webp"
              className="hidden"
            />

            {/* Overlapping Avatar with Center Camera Overlay */}
            <div className="px-6 flex justify-between items-end -mt-10 sm:-mt-12 mb-3">
              <div className="relative group">
                <div className="flex h-20 w-20 sm:h-22 sm:w-22 items-center justify-center rounded-full border-4 border-card bg-primary text-2xl font-bold text-primary-foreground shadow-md overflow-hidden flex-shrink-0">
                  {currentAvatarUrl ? (
                    <img
                      src={currentAvatarUrl}
                      alt={currentUser.displayName || currentUser.username}
                      className="h-full w-full object-cover"
                      onError={() => setCurrentAvatarUrl(null)}
                    />
                  ) : (
                    currentUser.displayName?.charAt(0).toUpperCase() ||
                    currentUser.username?.charAt(0).toUpperCase()
                  )}
                </div>

                {/* Avatar Camera Action Button */}
                <button
                  type="button"
                  onClick={() => fileInputRef.current?.click()}
                  disabled={isAnyProcessing}
                  className="absolute inset-0 flex items-center justify-center rounded-full bg-black/50 hover:bg-black/70 text-white transition-opacity disabled:pointer-events-none cursor-pointer"
                  title="Change Avatar Photo"
                >
                  {isUploading ? (
                    <Loader2 className="h-5 w-5 animate-spin" />
                  ) : (
                    <Camera className="h-5 w-5" />
                  )}
                </button>
              </div>

              {/* Gradient Color Swatches */}
              <div className="flex items-center space-x-1.5 pb-1">
                <span className="text-[11px] text-muted-foreground font-medium hidden sm:inline flex items-center mr-1">
                  <Sparkles className="h-3 w-3 mr-1 text-primary" />
                  Theme:
                </span>
                {BANNER_GRADIENTS.map((preset) => (
                  <button
                    key={preset.id}
                    type="button"
                    onClick={() => setCurrentBannerUrl(preset.id)}
                    className={`h-5 w-5 sm:h-6 sm:w-6 rounded-full shadow-xs transition-transform hover:scale-110 flex items-center justify-center cursor-pointer ${
                      currentBannerUrl === preset.id
                        ? "ring-2 ring-primary ring-offset-1 scale-105"
                        : "opacity-80 hover:opacity-100"
                    }`}
                    style={{ background: preset.gradient }}
                    title={preset.name}
                  >
                    {currentBannerUrl === preset.id && (
                      <Check className="h-3 w-3 text-white drop-shadow" />
                    )}
                  </button>
                ))}
              </div>
            </div>
          </div>

          {/* Form Fields Section */}
          <div className="px-6 space-y-4 pt-1">
            {/* Display Name Input */}
            <div className="space-y-1">
              <div className="flex justify-between items-center text-xs">
                <span className="font-medium text-foreground">Display Name</span>
                <span
                  className={
                    watchedDisplayName.length > 50
                      ? "text-destructive font-medium"
                      : "text-muted-foreground text-[11px]"
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
                      : "text-muted-foreground text-[11px]"
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
          </div>

          {/* Modal Footer Actions */}
          <div className="flex justify-end space-x-2 px-6 py-4 mt-4 border-t border-border bg-muted/10">
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
