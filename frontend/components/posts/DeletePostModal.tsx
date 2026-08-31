"use client";

import React, { useState } from "react";
import { Trash2, AlertTriangle } from "lucide-react";
import { Modal } from "../ui/modal";
import { Button } from "../ui/button";
import { postsApi } from "../../lib/api/posts";
import { useToast } from "../../hooks/useToast";

interface DeletePostModalProps {
  isOpen: boolean;
  onClose: () => void;
  postId: string;
  onDeleted: () => void;
}

export function DeletePostModal({
  isOpen,
  onClose,
  postId,
  onDeleted,
}: DeletePostModalProps) {
  const { success, error: toastError } = useToast();
  const [isDeleting, setIsDeleting] = useState(false);

  const handleDelete = async () => {
    setIsDeleting(true);
    try {
      await postsApi.deletePost(postId);
      success("Post deleted successfully.");
      onClose();
      onDeleted();
    } catch (err: unknown) {
      const message = err instanceof Error ? err.message : "Failed to delete post.";
      toastError(message);
    } finally {
      setIsDeleting(false);
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={() => !isDeleting && onClose()}
      title="Delete Post"
      description="Are you sure you want to delete this post? This action cannot be undone."
      className="max-w-md p-6"
    >
      <div className="space-y-4 pt-2">
        <div className="flex items-start space-x-3 rounded-lg border border-destructive/20 bg-destructive/10 p-3 text-xs text-destructive">
          <AlertTriangle className="h-5 w-5 flex-shrink-0" />
          <p>
            Your post and attached media will be removed from Celoteh Bebas. Historical replies will remain in thread context.
          </p>
        </div>

        <div className="flex justify-end space-x-2 pt-2">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onClose}
            disabled={isDeleting}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            onClick={handleDelete}
            isLoading={isDeleting}
            disabled={isDeleting}
          >
            <Trash2 className="mr-1.5 h-4 w-4" />
            Delete Post
          </Button>
        </div>
      </div>
    </Modal>
  );
}
