"use client";

import React from "react";

interface SearchHighlightProps {
  text?: string | null;
  fallbackText?: string;
  className?: string;
  highlightClassName?: string;
}

/**
 * Renders text with Elasticsearch highlighted terms (<mark> or <highlight>) safely.
 * ZERO XSS vulnerability: Does not use dangerouslySetInnerHTML.
 * Parses tags into React text elements and styled <mark> components.
 */
export function SearchHighlight({
  text,
  fallbackText = "",
  className = "",
  highlightClassName = "bg-primary/25 text-primary font-bold rounded px-0.5",
}: SearchHighlightProps) {
  const content = text ?? fallbackText;

  if (!content) {
    return null;
  }

  // Check if string contains any highlight tags
  const tagPattern = /<mark>(.*?)<\/mark>|<highlight>(.*?)<\/highlight>/gi;
  if (!tagPattern.test(content)) {
    return <span className={className}>{content}</span>;
  }

  // Reset regex state
  tagPattern.lastIndex = 0;

  const parts: React.ReactNode[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = tagPattern.exec(content)) !== null) {
    const matchIndex = match.index;

    // Push regular text preceding the match
    if (matchIndex > lastIndex) {
      parts.push(content.substring(lastIndex, matchIndex));
    }

    // Match group 1 (from <mark>) or group 2 (from <highlight>)
    const matchedTerm = match[1] ?? match[2] ?? "";
    parts.push(
      <mark
        key={`hl-${matchIndex}-${matchedTerm}`}
        className={highlightClassName}
      >
        {matchedTerm}
      </mark>
    );

    lastIndex = tagPattern.lastIndex;
  }

  // Push any remaining trailing text
  if (lastIndex < content.length) {
    parts.push(content.substring(lastIndex));
  }

  return <span className={className}>{parts}</span>;
}
