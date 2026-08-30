"use client";

import React, { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { editProfileSchema, type EditProfileFormData } from "../../lib/validations/auth";
import { useProfile } from "../../hooks/useProfile";
import { Modal } from "../ui/modal";
import { Input } from "../ui/input";
import { Button } from "../ui/button";
import type { User } from "../../types/auth";
import { ProblemDetailsException } from "../../lib/api/errors";

interface EditProfileModalProps {
  isOpen: boolean;
  onClose: () => void;
  currentUser: User;
}

export function EditProfileModal({ isOpen, onClose, currentUser }: EditProfileModalProps) {
  const { updateProfile, isUpdating } = useProfile();
  const [serverError, setServerError] = React.useState<string | null>(null);

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
      setServerError(null);
    }
  }, [isOpen, currentUser, reset]);

  const watchedDisplayName = watch("displayName") || "";
  const watchedBio = watch("bio") || "";

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
        setServerError(err.problem.detail || err.problem.title || "Failed to update profile.");
      } else if (err instanceof Error) {
        setServerError(err.message);
      } else {
        setServerError("An unexpected error occurred.");
      }
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Edit Profile"
      description="Update your public display name and biography."
    >
      <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
        {serverError && (
          <div
            role="alert"
            className="p-3 text-xs rounded-lg border border-destructive/50 bg-destructive/10 text-destructive font-medium"
          >
            {serverError}
          </div>
        )}

        <div className="space-y-1">
          <div className="flex justify-between items-center text-xs">
            <span className="font-medium text-foreground">Display Name</span>
            <span className={watchedDisplayName.length > 50 ? "text-destructive font-medium" : "text-muted-foreground"}>
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

        <div className="space-y-1">
          <div className="flex justify-between items-center text-xs">
            <span className="font-medium text-foreground">Biography</span>
            <span className={watchedBio.length > 160 ? "text-destructive font-medium" : "text-muted-foreground"}>
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
            <p className="text-xs font-medium text-destructive">{errors.bio.message}</p>
          )}
        </div>

        <div className="flex justify-end space-x-2 pt-3 border-t border-border">
          <Button type="button" variant="outline" size="sm" onClick={onClose} disabled={isUpdating}>
            Cancel
          </Button>
          <Button type="submit" variant="default" size="sm" isLoading={isUpdating}>
            Save Changes
          </Button>
        </div>
      </form>
    </Modal>
  );
}
