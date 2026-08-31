import { describe, it, expect } from "vitest";
import { formatPostTimestamp } from "../lib/utils/time";

describe("formatPostTimestamp utility", () => {
  it("should return 'Just now' for timestamps less than 1 minute ago", () => {
    const now = new Date();
    expect(formatPostTimestamp(now.toISOString())).toBe("Just now");

    const thirtySecondsAgo = new Date(now.getTime() - 30 * 1000);
    expect(formatPostTimestamp(thirtySecondsAgo.toISOString())).toBe("Just now");
  });

  it("should return relative minutes for timestamps between 1 and 59 minutes ago", () => {
    const now = new Date();
    const oneMinAgo = new Date(now.getTime() - 60 * 1000);
    expect(formatPostTimestamp(oneMinAgo.toISOString())).toBe("1 minute ago");

    const fifteenMinsAgo = new Date(now.getTime() - 15 * 60 * 1000);
    expect(formatPostTimestamp(fifteenMinsAgo.toISOString())).toBe("15 minutes ago");
  });

  it("should return relative hours for timestamps between 1 and 23 hours ago", () => {
    const now = new Date();
    const oneHourAgo = new Date(now.getTime() - 60 * 60 * 1000);
    expect(formatPostTimestamp(oneHourAgo.toISOString())).toBe("1 hour ago");

    const tenHoursAgo = new Date(now.getTime() - 10 * 60 * 60 * 1000);
    expect(formatPostTimestamp(tenHoursAgo.toISOString())).toBe("10 hours ago");
  });

  it("should return actual formatted date for timestamps 24 hours or older", () => {
    const now = new Date();
    const twoDaysAgo = new Date(now.getTime() - 48 * 60 * 60 * 1000);
    const result = formatPostTimestamp(twoDaysAgo.toISOString());

    expect(result).not.toContain("ago");
    expect(result.length).toBeGreaterThan(3);
  });
});
