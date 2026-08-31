"use client";

import React from "react";
import { ShieldAlert } from "lucide-react";
import { Modal } from "../ui/modal";
import { Button } from "../ui/button";

export interface BlockConfirmModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: () => Promise<void>;
  targetUsername: string;
  isLoading?: boolean;
}

export function BlockConfirmModal({
  isOpen,
  onClose,
  onConfirm,
  targetUsername,
  isLoading = false,
}: BlockConfirmModalProps) {
  const handleBlock = async () => {
    try {
      await onConfirm();
      onClose();
    } catch {
      // Error toast is handled by mutation hook
    }
  };

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title={`Block @${targetUsername}?`}
      className="max-w-md"
    >
      <div className="p-6 space-y-4">
        <div className="flex items-start space-x-3 p-3 rounded-lg bg-destructive/10 border border-destructive/20 text-destructive text-sm">
          <ShieldAlert className="h-5 w-5 mt-0.5 flex-shrink-0" />
          <div className="space-y-1">
            <p className="font-semibold">Blocking is reciprocal and restrictive:</p>
            <ul className="list-disc list-inside text-xs space-y-0.5 text-foreground/80">
              <li>They will not be able to follow you or view your posts.</li>
              <li>Existing follow relationships in both directions will be removed.</li>
              <li>They will not be notified that you blocked them.</li>
            </ul>
          </div>
        </div>

        <p className="text-xs text-muted-foreground">
          You can unblock this account at any time from their profile or your safety settings.
        </p>

        <div className="flex justify-end space-x-3 pt-3 border-t border-border">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onClose}
            disabled={isLoading}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant="destructive"
            size="sm"
            isLoading={isLoading}
            onClick={handleBlock}
          >
            Block @{targetUsername}
          </Button>
        </div>
      </div>
    </Modal>
  );
}
