"use client";

import React from "react";
import Link from "next/link";

interface HashtagTextProps {
  text?: string | null;
  className?: string;
}

// Regex matching backend specification:
// Starts with #, alphanumeric + underscore, boundary not preceded by word char, #, or @
const HASHTAG_REGEX = /(?<![#\w@])#([a-zA-Z0-9_]+)/g;

export function HashtagText({ text, className = "" }: HashtagTextProps) {
  if (!text) {
    return null;
  }

  const elements: React.ReactNode[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  // Reset regex state
  HASHTAG_REGEX.lastIndex = 0;

  while ((match = HASHTAG_REGEX.exec(text)) !== null) {
    const matchIndex = match.index;
    const fullMatch = match[0]; // e.g. "#CEBAS"
    const tag = match[1]; // e.g. "CEBAS"

    // Append preceding plain text
    if (matchIndex > lastIndex) {
      elements.push(text.substring(lastIndex, matchIndex));
    }

    const normalizedTag = tag.toLowerCase();

    // Render hashtag as accessible Next.js Link
    elements.push(
      <Link
        key={`${normalizedTag}-${matchIndex}`}
        href={`/tag/${encodeURIComponent(normalizedTag)}`}
        onClick={(e) => e.stopPropagation()}
        className="text-primary font-medium hover:underline focus:outline-none focus:ring-1 focus:ring-primary rounded px-0.5"
        aria-label={`Tag #${tag}`}
      >
        {fullMatch}
      </Link>
    );

    lastIndex = matchIndex + fullMatch.length;
  }

  // Append remaining text after the last match
  if (lastIndex < text.length) {
    elements.push(text.substring(lastIndex));
  }

  return <span className={className}>{elements}</span>;
}
