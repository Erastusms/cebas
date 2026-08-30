import { useUiStore, type ToastVariant } from "../stores/useUiStore";

export function useToast() {
  const addToast = useUiStore((state) => state.addToast);
  const removeToast = useUiStore((state) => state.removeToast);
  const toasts = useUiStore((state) => state.toasts);

  return {
    toasts,
    toast: (description: string, options?: { title?: string; variant?: ToastVariant; durationMs?: number }) =>
      addToast({ description, ...options }),
    success: (description: string, title?: string) =>
      addToast({ description, title, variant: "success" }),
    error: (description: string, title?: string) =>
      addToast({ description, title, variant: "error" }),
    dismiss: removeToast,
  };
}
