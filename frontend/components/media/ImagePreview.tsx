"use client";

import React, { useState } from "react";
import { X, AlertCircle } from "lucide-react";
import { Button } from "../ui/button";

interface ImagePreviewProps {
  src: string | null;
  alt?: string;
  onClear?: () => void;
  className?: string;
  size?: "sm" | "md" | "lg";
}

export function ImagePreview({
  src,
  alt = "Preview",
  onClear,
  className = "",
  size = "md",
}: ImagePreviewProps) {
  const [hasError, setHasError] = useState(false);

  if (!src) {
    return null;
  }

  const sizeClasses = {
    sm: "h-16 w-16",
    md: "h-28 w-28",
    lg: "h-40 w-40",
  }[size];

  return (
    <div className={`relative inline-block ${className}`}>
      <div
        className={`relative overflow-hidden rounded-full border-2 border-border bg-muted ${sizeClasses}`}
      >
        {hasError ? (
          <div className="flex h-full w-full flex-col items-center justify-center p-2 text-center text-muted-foreground">
            <AlertCircle className="h-6 w-6 text-destructive" />
            <span className="mt-1 text-[10px]">Failed to load</span>
          </div>
        ) : (
          <img
            src={src}
            alt={alt}
            onError={() => setHasError(true)}
            className="h-full w-full object-cover"
          />
        )}
      </div>

      {onClear && (
        <Button
          type="button"
          variant="destructive"
          size="sm"
          onClick={onClear}
          className="absolute -top-1 -right-1 h-6 w-6 rounded-full p-0 shadow-md"
          title="Remove image"
        >
          <X className="h-3.5 w-3.5" />
        </Button>
      )}
    </div>
  );
}
