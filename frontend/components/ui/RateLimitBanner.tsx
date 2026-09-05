"use client";

import React from "react";
import { AlertTriangle, Clock, X } from "lucide-react";
import { useRateLimitStore } from "../../stores/useRateLimitStore";

export function RateLimitBanner() {
  const { isRateLimited, retryAfterSeconds, clearRateLimit } = useRateLimitStore();

  if (!isRateLimited) {
    return null;
  }

  return (
    <aside
      aria-live="polite"
      role="alert"
      className="fixed bottom-4 right-4 z-50 max-w-md animate-in fade-in slide-in-from-bottom-5 duration-300"
    >
      <div className="flex items-center gap-3 rounded-2xl border border-amber-500/30 bg-amber-500/10 p-4 shadow-xl backdrop-blur-md dark:bg-amber-950/40">
        <div className="flex h-10 w-10 flex-shrink-0 items-center justify-center rounded-xl bg-amber-500/20 text-amber-600 dark:text-amber-400">
          <AlertTriangle className="h-5 w-5" />
        </div>
        <div className="flex-1 text-sm">
          <p className="font-semibold text-foreground">
            You&apos;re doing that a little too quickly
          </p>
          <div className="mt-0.5 flex items-center gap-1.5 text-xs text-muted-foreground">
            <Clock className="h-3.5 w-3.5 text-amber-500" />
            <span>
              Please try again in{" "}
              <strong className="font-bold text-amber-600 dark:text-amber-400">
                {retryAfterSeconds}s
              </strong>
            </span>
          </div>
        </div>
        <button
          type="button"
          onClick={clearRateLimit}
          className="rounded-lg p-1 text-muted-foreground hover:bg-muted hover:text-foreground"
          aria-label="Dismiss rate limit banner"
        >
          <X className="h-4 w-4" />
        </button>
      </div>
    </aside>
  );
}
