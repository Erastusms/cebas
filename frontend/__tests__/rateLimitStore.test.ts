import { describe, it, expect, beforeEach, afterEach, vi } from "vitest";
import { useRateLimitStore } from "../stores/useRateLimitStore";

describe("Rate Limit Zustand Store", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    useRateLimitStore.getState().clearRateLimit();
  });

  afterEach(() => {
    useRateLimitStore.getState().clearRateLimit();
    vi.useRealTimers();
  });

  it("should initialize with default inactive rate limit state", () => {
    const state = useRateLimitStore.getState();
    expect(state.isRateLimited).toBe(false);
    expect(state.retryAfterSeconds).toBe(0);
    expect(state.message).toBe("");
    expect(state.scope).toBe("global");
    expect(state.timerId).toBeNull();
  });

  it("should activate rate limit and set countdown when setRateLimited is called", () => {
    useRateLimitStore.getState().setRateLimited(30, "pub", "Posting too fast");

    const state = useRateLimitStore.getState();
    expect(state.isRateLimited).toBe(true);
    expect(state.retryAfterSeconds).toBe(30);
    expect(state.scope).toBe("pub");
    expect(state.message).toBe("Posting too fast");
    expect(state.timerId).not.toBeNull();
  });

  it("should decrement remaining seconds every second", () => {
    useRateLimitStore.getState().setRateLimited(10, "auth");

    expect(useRateLimitStore.getState().retryAfterSeconds).toBe(10);

    vi.advanceTimersByTime(1000);
    expect(useRateLimitStore.getState().retryAfterSeconds).toBe(9);

    vi.advanceTimersByTime(3000);
    expect(useRateLimitStore.getState().retryAfterSeconds).toBe(6);
  });

  it("should automatically reset rate limit state when countdown reaches zero", () => {
    useRateLimitStore.getState().setRateLimited(3, "eng");

    expect(useRateLimitStore.getState().isRateLimited).toBe(true);

    vi.advanceTimersByTime(3000);

    const state = useRateLimitStore.getState();
    expect(state.isRateLimited).toBe(false);
    expect(state.retryAfterSeconds).toBe(0);
    expect(state.message).toBe("");
    expect(state.timerId).toBeNull();
  });

  it("should immediately reset when clearRateLimit is invoked manually", () => {
    useRateLimitStore.getState().setRateLimited(45, "rep");
    expect(useRateLimitStore.getState().isRateLimited).toBe(true);

    useRateLimitStore.getState().clearRateLimit();

    const state = useRateLimitStore.getState();
    expect(state.isRateLimited).toBe(false);
    expect(state.retryAfterSeconds).toBe(0);
    expect(state.timerId).toBeNull();
  });
});
