/**
 * Formats a timestamp into relative time if less than 24 hours old (e.g. "Just now", "3 minutes ago", "1 hour ago"),
 * or actual formatted date if 24 hours or older.
 */
export function formatPostTimestamp(dateInput: string | Date | null | undefined): string {
  if (!dateInput) return "";

  const date = typeof dateInput === "string" ? new Date(dateInput) : dateInput;
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();

  // Handle minor clock skew / future timestamps
  if (diffMs < 0) {
    return "Just now";
  }

  const diffSec = Math.floor(diffMs / 1000);
  const diffMin = Math.floor(diffSec / 60);
  const diffHours = Math.floor(diffMin / 60);

  // Less than 24 hours old -> relative timestamp
  if (diffHours < 24) {
    if (diffSec < 60) {
      return "Just now";
    }
    if (diffMin < 60) {
      return diffMin === 1 ? "1 minute ago" : `${diffMin} minutes ago`;
    }
    return diffHours === 1 ? "1 hour ago" : `${diffHours} hours ago`;
  }

  // More than 24 hours old -> actual formatted date
  return date.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: date.getFullYear() !== now.getFullYear() ? "numeric" : undefined,
    hour: "2-digit",
    minute: "2-digit",
  });
}
