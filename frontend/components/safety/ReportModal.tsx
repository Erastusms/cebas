"use client";

import React, { useState, useEffect, useRef } from "react";
import { Flag, X, AlertTriangle, ShieldCheck, Loader2 } from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { safetyApi } from "../../lib/api/safety";
import { useToast } from "../../hooks/useToast";
import { ProblemDetailsException } from "../../lib/api/errors";
import { Button } from "../ui/button";

export interface ReportModalProps {
  isOpen: boolean;
  onClose: () => void;
  targetPostId?: string | null;
  targetUserId?: string | null;
  targetName?: string; // post author or username being reported
}

const REPORT_CATEGORIES = [
  {
    id: "SPAM",
    label: "Spam",
    description: "Malicious links, automated posts, fake engagement, or commercial scams.",
  },
  {
    id: "HARASSMENT",
    label: "Harassment",
    description: "Targeted insults, bullying, stalking, threats, or intimidation.",
  },
  {
    id: "HATE_SPEECH",
    label: "Hate Speech",
    description: "Discrimination, dehumanizing slurs, or hatred targeting protected groups.",
  },
  {
    id: "INAPPROPRIATE_CONTENT",
    label: "Inappropriate Content",
    description: "Graphic violence, illegal material, sexual exploitation, or sensitive abuse.",
  },
] as const;

export function ReportModal({
  isOpen,
  onClose,
  targetPostId,
  targetUserId,
  targetName,
}: ReportModalProps) {
  const queryClient = useQueryClient();
  const [selectedCategory, setSelectedCategory] = useState<string>("");
  const [description, setDescription] = useState<string>("");
  const [isSubmitting, setIsSubmitting] = useState<boolean>(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [isSuccess, setIsSuccess] = useState<boolean>(false);
  const { success, error: showErrorToast } = useToast();
  const modalRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isOpen) {
      setSelectedCategory("");
      setDescription("");
      setErrorMessage(null);
      setIsSuccess(false);
    }
  }, [isOpen]);

  // Keyboard accessibility: ESC key to close
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && isOpen && !isSubmitting) {
        onClose();
      }
    };
    if (isOpen) {
      window.addEventListener("keydown", handleKeyDown);
    }
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, isSubmitting, onClose]);

  if (!isOpen) return null;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedCategory) {
      setErrorMessage("Please select a report category.");
      return;
    }

    setIsSubmitting(true);
    setErrorMessage(null);

    try {
      await safetyApi.createReport({
        targetPostId: targetPostId || null,
        targetUserId: targetUserId || null,
        category: selectedCategory,
        description: description.trim() || null,
      });

      queryClient.invalidateQueries({ queryKey: ["adminReports"] });

      setIsSuccess(true);
      success(
        "Thank you for keeping CEBAS safe. Your report has been submitted to our moderation team.",
        "Report Submitted"
      );

      setTimeout(() => {
        onClose();
      }, 1500);
    } catch (err: unknown) {
      if (err instanceof ProblemDetailsException) {
        setErrorMessage(err.message);
      } else if (err instanceof Error) {
        setErrorMessage(err.message);
      } else {
        setErrorMessage("Failed to submit report. Please try again later.");
      }
      showErrorToast("Could not submit report. Please review the error.", "Submission Failed");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm animate-in fade-in duration-200"
      onClick={(e) => {
        if (e.target === e.currentTarget && !isSubmitting) {
          onClose();
        }
      }}
      role="dialog"
      aria-modal="true"
      aria-labelledby="report-modal-title"
    >
      <div
        ref={modalRef}
        className="w-full max-w-lg rounded-2xl border border-border bg-card p-6 shadow-2xl space-y-5 animate-in zoom-in-95 duration-200"
      >
        {/* Header */}
        <div className="flex items-center justify-between border-b border-border pb-4">
          <div className="flex items-center space-x-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-destructive/10 text-destructive">
              <Flag className="h-5 w-5" />
            </div>
            <div>
              <h2 id="report-modal-title" className="text-base font-bold text-foreground">
                Report {targetPostId ? "Post" : "User"}
              </h2>
              {targetName && (
                <p className="text-xs text-muted-foreground">
                  Target: {targetName}
                </p>
              )}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            disabled={isSubmitting}
            className="rounded-lg p-1 text-muted-foreground hover:bg-muted hover:text-foreground disabled:opacity-50"
            aria-label="Close report modal"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {isSuccess ? (
          <div className="py-8 text-center space-y-3 animate-in fade-in duration-300">
            <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-500/10 text-emerald-500">
              <ShieldCheck className="h-8 w-8" />
            </div>
            <h3 className="text-base font-bold text-foreground">Report Received</h3>
            <p className="text-xs text-muted-foreground max-w-sm mx-auto">
              Our moderation team will review this report against CEBAS Community Standards. Thank you for your contribution.
            </p>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            {errorMessage && (
              <div
                role="alert"
                className="flex items-center space-x-2.5 rounded-xl border border-destructive/30 bg-destructive/10 p-3 text-xs text-destructive"
              >
                <AlertTriangle className="h-4 w-4 flex-shrink-0" />
                <span>{errorMessage}</span>
              </div>
            )}

            {/* Category selection */}
            <div className="space-y-2">
              <label className="text-xs font-semibold text-foreground">
                Why are you reporting this? <span className="text-destructive">*</span>
              </label>
              <div className="grid gap-2">
                {REPORT_CATEGORIES.map((cat) => {
                  const isChecked = selectedCategory === cat.id;
                  return (
                    <label
                      key={cat.id}
                      className={`flex items-start space-x-3 rounded-xl border p-3 cursor-pointer transition ${
                        isChecked
                          ? "border-primary bg-primary/5 ring-1 ring-primary"
                          : "border-border hover:bg-muted/50"
                      }`}
                    >
                      <input
                        type="radio"
                        name="reportCategory"
                        value={cat.id}
                        checked={isChecked}
                        onChange={() => setSelectedCategory(cat.id)}
                        className="mt-0.5 text-primary focus:ring-primary h-4 w-4"
                      />
                      <div className="text-xs">
                        <span className="font-semibold text-foreground block">
                          {cat.label}
                        </span>
                        <span className="text-muted-foreground block leading-relaxed">
                          {cat.description}
                        </span>
                      </div>
                    </label>
                  );
                })}
              </div>
            </div>

            {/* Additional details */}
            <div className="space-y-1.5">
              <div className="flex justify-between items-center text-xs">
                <label htmlFor="report-description" className="font-semibold text-foreground">
                  Additional Context (Optional)
                </label>
                <span className="text-muted-foreground text-[11px]">
                  {description.length}/1000
                </span>
              </div>
              <textarea
                id="report-description"
                rows={3}
                maxLength={1000}
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Provide any additional context or details that will help our moderators..."
                className="w-full rounded-xl border border-border bg-background p-3 text-xs placeholder:text-muted-foreground focus:border-primary focus:outline-none focus:ring-1 focus:ring-primary"
              />
            </div>

            {/* Footer Buttons */}
            <div className="flex items-center justify-end space-x-2 pt-2 border-t border-border">
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={onClose}
                disabled={isSubmitting}
              >
                Cancel
              </Button>
              <Button
                type="submit"
                variant="destructive"
                size="sm"
                disabled={!selectedCategory || isSubmitting}
                className="min-w-[100px]"
              >
                {isSubmitting ? (
                  <>
                    <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                    Submitting...
                  </>
                ) : (
                  "Submit Report"
                )}
              </Button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
