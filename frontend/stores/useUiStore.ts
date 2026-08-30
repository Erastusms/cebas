import { create } from "zustand";

export type ToastVariant = "default" | "success" | "error" | "warning" | "info";

export type ToastItem = {
  id: string;
  title?: string;
  description: string;
  variant?: ToastVariant;
  durationMs?: number;
};

type UiState = {
  toasts: ToastItem[];
  isModalOpen: boolean;
  activeModalId: string | null;
  addToast: (toast: Omit<ToastItem, "id">) => string;
  removeToast: (id: string) => void;
  openModal: (modalId: string) => void;
  closeModal: () => void;
};

export const useUiStore = create<UiState>((set) => ({
  toasts: [],
  isModalOpen: false,
  activeModalId: null,

  addToast: (toast) => {
    const id = Math.random().toString(36).substring(2, 9);
    const newToast: ToastItem = { id, durationMs: 4000, ...toast };

    set((state) => ({
      toasts: [...state.toasts, newToast],
    }));

    if (newToast.durationMs && newToast.durationMs > 0) {
      setTimeout(() => {
        set((state) => ({
          toasts: state.toasts.filter((t) => t.id !== id),
        }));
      }, newToast.durationMs);
    }

    return id;
  },

  removeToast: (id) =>
    set((state) => ({
      toasts: state.toasts.filter((t) => t.id !== id),
    })),

  openModal: (modalId) => set({ isModalOpen: true, activeModalId: modalId }),
  closeModal: () => set({ isModalOpen: false, activeModalId: null }),
}));
