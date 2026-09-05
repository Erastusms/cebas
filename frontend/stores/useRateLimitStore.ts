import { create } from "zustand";

export interface RateLimitState {
  isRateLimited: boolean;
  retryAfterSeconds: number;
  message: string;
  scope: string; // e.g. "auth", "pub", "eng", "rep", "global"
  timerId: NodeJS.Timeout | null;
  setRateLimited: (retryAfter: number, scope?: string, message?: string) => void;
  clearRateLimit: () => void;
}

export const useRateLimitStore = create<RateLimitState>((set, get) => ({
  isRateLimited: false,
  retryAfterSeconds: 0,
  message: "",
  scope: "global",
  timerId: null,

  setRateLimited: (retryAfter: number, scope = "global", customMessage) => {
    const existingTimer = get().timerId;
    if (existingTimer) {
      clearInterval(existingTimer);
    }

    const duration = Math.max(1, retryAfter);
    const msg = customMessage || `You're doing that a little too quickly. Please try again in ${duration} seconds.`;

    set({
      isRateLimited: true,
      retryAfterSeconds: duration,
      message: msg,
      scope,
    });

    const intervalId = setInterval(() => {
      const currentRemaining = get().retryAfterSeconds;
      if (currentRemaining <= 1) {
        clearInterval(intervalId);
        set({
          isRateLimited: false,
          retryAfterSeconds: 0,
          message: "",
          timerId: null,
        });
      } else {
        set({
          retryAfterSeconds: currentRemaining - 1,
          message: `You're doing that a little too quickly. Please try again in ${currentRemaining - 1} seconds.`,
        });
      }
    }, 1000);

    set({ timerId: intervalId });
  },

  clearRateLimit: () => {
    const existingTimer = get().timerId;
    if (existingTimer) {
      clearInterval(existingTimer);
    }
    set({
      isRateLimited: false,
      retryAfterSeconds: 0,
      message: "",
      timerId: null,
    });
  },
}));
