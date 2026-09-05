"use client";

import React from "react";
import { ArrowUp, Sparkles } from "lucide-react";

interface NewPostsBannerProps {
  count: number;
  onClick: () => void;
}

export function NewPostsBanner({ count, onClick }: NewPostsBannerProps) {
  if (count <= 0) return null;

  return (
    <div className="sticky top-20 z-30 flex justify-center my-2 animate-in fade-in slide-in-from-top-3 duration-300 pointer-events-none">
      <button
        type="button"
        onClick={onClick}
        className="pointer-events-auto flex items-center space-x-2 rounded-full bg-primary px-4 py-2 text-xs sm:text-sm font-semibold text-primary-foreground shadow-lg hover:bg-primary/90 focus:outline-none focus:ring-2 focus:ring-primary/50 transition-all hover:scale-105 active:scale-95"
      >
        <Sparkles className="h-3.5 w-3.5" />
        <span>
          Ada {count} celotehan baru
        </span>
        <ArrowUp className="h-3.5 w-3.5" />
      </button>
    </div>
  );
}
