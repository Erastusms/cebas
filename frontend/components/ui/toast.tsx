"use client";

import React from "react";
import { CheckCircle2, AlertCircle, AlertTriangle, Info, X } from "lucide-react";
import { useUiStore, type ToastItem, type ToastVariant } from "../../stores/useUiStore";
import { cn } from "../../lib/utils";

const variantIcons: Record<ToastVariant, React.ReactNode> = {
  default: <Info className="h-5 w-5 text-foreground" />,
  info: <Info className="h-5 w-5 text-primary" />,
  success: <CheckCircle2 className="h-5 w-5 text-emerald-500" />,
  error: <AlertCircle className="h-5 w-5 text-destructive" />,
  warning: <AlertTriangle className="h-5 w-5 text-amber-500" />,
};

export function ToastContainer() {
  const toasts = useUiStore((state) => state.toasts);
  const removeToast = useUiStore((state) => state.removeToast);

  if (toasts.length === 0) return null;

  return (
    <div
      aria-live="polite"
      aria-atomic="true"
      className="fixed bottom-4 right-4 z-50 flex flex-col gap-2 max-w-md w-full px-4 pointer-events-none"
    >
      {toasts.map((toast) => (
        <ToastCard key={toast.id} toast={toast} onDismiss={() => removeToast(toast.id)} />
      ))}
    </div>
  );
}

function ToastCard({ toast, onDismiss }: { toast: ToastItem; onDismiss: () => void }) {
  const variant = toast.variant ?? "default";

  return (
    <div
      className={cn(
        "pointer-events-auto flex items-start gap-3 rounded-xl border border-border bg-card p-4 shadow-lg text-card-foreground transition-all animate-in slide-in-from-bottom-5",
        variant === "error" && "border-destructive/40",
        variant === "success" && "border-emerald-500/40"
      )}
      role="alert"
    >
      <div className="flex-shrink-0 mt-0.5">{variantIcons[variant]}</div>
      <div className="flex-1 text-sm text-left">
        {toast.title && <h4 className="font-semibold text-foreground">{toast.title}</h4>}
        <p className="text-muted-foreground">{toast.description}</p>
      </div>
      <button
        onClick={onDismiss}
        aria-label="Dismiss notification"
        className="rounded-md p-1 text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
      >
        <X className="h-4 w-4" />
      </button>
    </div>
  );
}
