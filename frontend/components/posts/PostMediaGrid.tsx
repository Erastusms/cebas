"use client";

import React, { useState, useEffect, useCallback, useRef } from "react";
import { X, ChevronLeft, ChevronRight } from "lucide-react";
import type { PostMedia } from "../../types/api";

interface PostMediaGridProps {
  media: PostMedia[];
  className?: string;
}

export function PostMediaGrid({ media, className = "" }: PostMediaGridProps) {
  const [selectedIndex, setSelectedIndex] = useState<number | null>(null);
  const touchStartX = useRef<number | null>(null);
  const touchEndX = useRef<number | null>(null);

  const count = media?.length || 0;

  const handlePrev = useCallback(() => {
    setSelectedIndex((prev) => (prev !== null && prev > 0 ? prev - 1 : prev));
  }, []);

  const handleNext = useCallback(() => {
    setSelectedIndex((prev) => (prev !== null && prev < count - 1 ? prev + 1 : prev));
  }, [count]);

  const handleClose = useCallback(() => {
    setSelectedIndex(null);
  }, []);

  // Keyboard navigation: Left/Right arrows and Escape
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (selectedIndex === null) return;

      if (e.key === "Escape") {
        handleClose();
      } else if (e.key === "ArrowLeft") {
        handlePrev();
      } else if (e.key === "ArrowRight") {
        handleNext();
      }
    };

    if (selectedIndex !== null) {
      window.addEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "hidden";
    }

    return () => {
      window.removeEventListener("keydown", handleKeyDown);
      document.body.style.overflow = "unset";
    };
  }, [selectedIndex, handleClose, handlePrev, handleNext]);

  // Touch swipe support for mobile sliding
  const handleTouchStart = (e: React.TouchEvent) => {
    touchStartX.current = e.targetTouches[0].clientX;
  };

  const handleTouchMove = (e: React.TouchEvent) => {
    touchEndX.current = e.targetTouches[0].clientX;
  };

  const handleTouchEnd = () => {
    if (!touchStartX.current || !touchEndX.current) return;
    const distance = touchStartX.current - touchEndX.current;
    const minSwipeDistance = 50;

    if (distance > minSwipeDistance) {
      // Swiped Left -> Next image
      handleNext();
    } else if (distance < -minSwipeDistance) {
      // Swiped Right -> Previous image
      handlePrev();
    }

    touchStartX.current = null;
    touchEndX.current = null;
  };

  if (!media || count === 0) return null;

  return (
    <>
      <div className={`overflow-hidden rounded-2xl border border-border bg-muted/20 ${className}`}>
        {count === 1 && (
          <div className="relative aspect-auto max-h-[500px] w-full overflow-hidden">
            <img
              src={media[0].url}
              alt={media[0].originalFileName || "Post image"}
              className="h-full w-full object-cover cursor-pointer transition hover:opacity-95"
              onClick={() => setSelectedIndex(0)}
            />
          </div>
        )}

        {count === 2 && (
          <div className="grid grid-cols-2 gap-1">
            {media.map((item, idx) => (
              <div key={item.id || idx} className="relative aspect-[4/3] w-full overflow-hidden">
                <img
                  src={item.url}
                  alt={item.originalFileName || `Post image ${idx + 1}`}
                  className="h-full w-full object-cover cursor-pointer transition hover:opacity-95"
                  onClick={() => setSelectedIndex(idx)}
                />
              </div>
            ))}
          </div>
        )}

        {count === 3 && (
          <div className="grid grid-cols-2 gap-1">
            <div className="relative aspect-[4/3] w-full overflow-hidden row-span-2">
              <img
                src={media[0].url}
                alt={media[0].originalFileName || "Post image 1"}
                className="h-full w-full object-cover cursor-pointer transition hover:opacity-95"
                onClick={() => setSelectedIndex(0)}
              />
            </div>
            <div className="space-y-1">
              <div className="relative aspect-[16/9] w-full overflow-hidden">
                <img
                  src={media[1].url}
                  alt={media[1].originalFileName || "Post image 2"}
                  className="h-full w-full object-cover cursor-pointer transition hover:opacity-95"
                  onClick={() => setSelectedIndex(1)}
                />
              </div>
              <div className="relative aspect-[16/9] w-full overflow-hidden">
                <img
                  src={media[2].url}
                  alt={media[2].originalFileName || "Post image 3"}
                  className="h-full w-full object-cover cursor-pointer transition hover:opacity-95"
                  onClick={() => setSelectedIndex(2)}
                />
              </div>
            </div>
          </div>
        )}

        {count >= 4 && (
          <div className="grid grid-cols-2 gap-1">
            {media.slice(0, 4).map((item, idx) => (
              <div key={item.id || idx} className="relative aspect-[4/3] w-full overflow-hidden">
                <img
                  src={item.url}
                  alt={item.originalFileName || `Post image ${idx + 1}`}
                  className="h-full w-full object-cover cursor-pointer transition hover:opacity-95"
                  onClick={() => setSelectedIndex(idx)}
                />
              </div>
            ))}
          </div>
        )}
      </div>

      {/* Full-screen Lightbox Modal with Slider Animation */}
      {selectedIndex !== null && (
        <div
          role="dialog"
          aria-modal="true"
          aria-label="Image preview"
          className="fixed inset-0 z-50 flex items-center justify-center bg-black/90 p-2 sm:p-4 backdrop-blur-md animate-in fade-in duration-200"
          onClick={handleClose}
        >
          {/* Close button */}
          <button
            type="button"
            onClick={handleClose}
            className="absolute right-4 top-4 z-50 rounded-full bg-black/60 p-2.5 text-white transition hover:bg-black/80 focus:outline-none focus:ring-2 focus:ring-primary"
            aria-label="Close image preview"
          >
            <X className="h-6 w-6" />
          </button>

          {/* Previous image button */}
          {count > 1 && selectedIndex > 0 && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                handlePrev();
              }}
              className="absolute left-3 sm:left-6 top-1/2 z-50 -translate-y-1/2 rounded-full bg-black/60 p-3 text-white transition hover:bg-black/85 hover:scale-110 focus:outline-none focus:ring-2 focus:ring-primary shadow-xl"
              aria-label="Previous image"
            >
              <ChevronLeft className="h-7 w-7" />
            </button>
          )}

          {/* Next image button */}
          {count > 1 && selectedIndex < count - 1 && (
            <button
              type="button"
              onClick={(e) => {
                e.stopPropagation();
                handleNext();
              }}
              className="absolute right-3 sm:right-6 top-1/2 z-50 -translate-y-1/2 rounded-full bg-black/60 p-3 text-white transition hover:bg-black/85 hover:scale-110 focus:outline-none focus:ring-2 focus:ring-primary shadow-xl"
              aria-label="Next image"
            >
              <ChevronRight className="h-7 w-7" />
            </button>
          )}

          {/* Slide Track Container */}
          <div
            className="relative flex h-[85vh] w-[90vw] max-w-5xl items-center justify-center overflow-hidden"
            onClick={(e) => e.stopPropagation()}
            onTouchStart={handleTouchStart}
            onTouchMove={handleTouchMove}
            onTouchEnd={handleTouchEnd}
          >
            <div
              className="flex h-full w-full transition-transform duration-300 ease-out will-change-transform"
              style={{ transform: `translateX(-${selectedIndex * 100}%)` }}
            >
              {media.map((item, idx) => (
                <div
                  key={item.id || idx}
                  className="flex h-full w-full flex-shrink-0 items-center justify-center p-2 sm:p-4 select-none"
                >
                  <img
                    src={item.url}
                    alt={item.originalFileName || `Preview ${idx + 1}`}
                    className="max-h-[80vh] max-w-full rounded-lg object-contain shadow-2xl pointer-events-none select-none"
                    draggable={false}
                  />
                </div>
              ))}
            </div>
          </div>

          {/* Image index badge indicator */}
          {count > 1 && (
            <div className="absolute bottom-6 left-1/2 -translate-x-1/2 rounded-full bg-black/60 px-4 py-1.5 text-xs font-semibold text-white backdrop-blur-sm shadow-md">
              {selectedIndex + 1} / {count}
            </div>
          )}
        </div>
      )}
    </>
  );
}
