"use client";

import React, { useState } from "react";
import { Send, X, AlertCircle } from "lucide-react";
import { Button } from "../ui/button";
import { postsApi } from "../../lib/api/posts";
import { useToast } from "../../hooks/useToast";
import type { ReplyItem } from "../../types/api";

interface ReplyComposerProps {
  postId: string;
  parentReplyId?: string | null;
  replyingToUsername?: string | null;
  onReplyCreated?: (reply: ReplyItem) => void;
  onCancel?: () => void;
  className?: string;
  placeholder?: string;
}

export function ReplyComposer({
  postId,
  parentReplyId = null,
  replyingToUsername,
  onReplyCreated,
  onCancel,
  className = "",
  placeholder = "Write a reply...",
}: ReplyComposerProps) {
  const { success, error: toastError } = useToast();
  const [content, setContent] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  const maxChars = 1000;
  const charsRemaining = maxChars - content.length;
  const isOverLimit = charsRemaining < 0;
  const canSubmit = !isSubmitting && !isOverLimit && content.trim().length > 0;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!canSubmit) return;

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      const response = await postsApi.createReply(postId, {
        content: content.trim(),
        parentReplyId: parentReplyId || undefined,
      });

      success("Reply posted!");
      setContent("");
      if (onReplyCreated) {
        onReplyCreated(response.data);
      }
      if (onCancel) {
        onCancel();
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to post reply.";
      setErrorMessage(msg);
      toastError(msg);
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className={`rounded-2xl border border-border bg-card p-4 shadow-sm ${className}`}>
      {replyingToUsername && (
        <div className="mb-2 flex items-center justify-between text-xs text-muted-foreground">
          <div className="flex items-center space-x-1">
            <span>Replying to</span>
            <span className="font-semibold text-primary">@{replyingToUsername}</span>
          </div>
          {onCancel && (
            <button
              type="button"
              onClick={onCancel}
              className="flex items-center space-x-0.5 text-xs text-muted-foreground hover:text-foreground transition"
              aria-label="Cancel reply"
            >
              <X className="h-3.5 w-3.5" />
              <span>Cancel</span>
            </button>
          )}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-3">
        <textarea
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder={placeholder}
          rows={2}
          disabled={isSubmitting}
          maxLength={1100}
          style={{ outline: "none", border: "none", boxShadow: "none" }}
          className="w-full resize-none border-none bg-transparent p-0 text-sm text-foreground placeholder:text-muted-foreground outline-none focus:outline-none focus:ring-0 focus-visible:ring-0 focus-visible:outline-none shadow-none"
          aria-label="Reply content"
        />

        {errorMessage && (
          <div className="flex items-center space-x-2 rounded-lg bg-destructive/10 p-2.5 text-xs text-destructive">
            <AlertCircle className="h-4 w-4 flex-shrink-0" />
            <span>{errorMessage}</span>
          </div>
        )}

        <div className="flex items-center justify-between border-t border-border pt-2">
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

          <div className="flex items-center space-x-2">
            {onCancel && !replyingToUsername && (
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={onCancel}
                disabled={isSubmitting}
              >
                Cancel
              </Button>
            )}

            <Button
              type="submit"
              variant="default"
              size="sm"
              disabled={!canSubmit}
              isLoading={isSubmitting}
              className="font-medium"
            >
              <Send className="mr-1.5 h-3.5 w-3.5" />
              <span>Reply</span>
            </Button>
          </div>
        </div>
      </form>
    </div>
  );
}
