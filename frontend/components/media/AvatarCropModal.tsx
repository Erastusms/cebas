"use client";

import React, { useState, useRef, useEffect, useCallback } from "react";
import { ZoomOut, RotateCcw, Check } from "lucide-react";
import { Modal } from "../ui/modal";
import { Button } from "../ui/button";

interface AvatarCropModalProps {
  isOpen: boolean;
  onClose: () => void;
  imageFile: File | Blob | null;
  onConfirmCrop: (croppedBlob: Blob) => Promise<void> | void;
  isProcessing?: boolean;
}

export function AvatarCropModal({
  isOpen,
  onClose,
  imageFile,
  onConfirmCrop,
  isProcessing = false,
}: AvatarCropModalProps) {
  const [imageSrc, setImageSrc] = useState<string | null>(null);
  const [zoom, setZoom] = useState(1);
  const [pan, setPan] = useState({ x: 0, y: 0 });
  const [isDragging, setIsDragging] = useState(false);
  const [dragStart, setDragStart] = useState({ x: 0, y: 0 });

  const imgRef = useRef<HTMLImageElement | null>(null);

  // Load image preview URL when imageFile changes
  useEffect(() => {
    if (!imageFile) {
      setImageSrc(null);
      return;
    }

    const url = URL.createObjectURL(imageFile);
    setImageSrc(url);
    setZoom(1);
    setPan({ x: 0, y: 0 });

    const img = new window.Image();
    img.src = url;
    img.onload = () => {
      imgRef.current = img;
    };

    return () => {
      URL.revokeObjectURL(url);
    };
  }, [imageFile]);

  // Handle Drag / Pan
  const handleMouseDown = (e: React.MouseEvent) => {
    setIsDragging(true);
    setDragStart({ x: e.clientX - pan.x, y: e.clientY - pan.y });
  };

  const handleMouseMove = (e: React.MouseEvent) => {
    if (!isDragging) return;
    setPan({
      x: e.clientX - dragStart.x,
      y: e.clientY - dragStart.y,
    });
  };

  const handleMouseUp = () => {
    setIsDragging(false);
  };

  const handleTouchStart = (e: React.TouchEvent) => {
    if (e.touches.length === 1) {
      setIsDragging(true);
      setDragStart({
        x: e.touches[0].clientX - pan.x,
        y: e.touches[0].clientY - pan.y,
      });
    }
  };

  const handleTouchMove = (e: React.TouchEvent) => {
    if (!isDragging || e.touches.length !== 1) return;
    setPan({
      x: e.touches[0].clientX - dragStart.x,
      y: e.touches[0].clientY - dragStart.y,
    });
  };

  const handleTouchEnd = () => {
    setIsDragging(false);
  };

  const handleReset = () => {
    setZoom(1);
    setPan({ x: 0, y: 0 });
  };

  // Perform crop on canvas and export as WebP/JPEG Blob
  const handleApply = useCallback(() => {
    if (!imgRef.current) return;

    const canvas = document.createElement("canvas");
    const outputSize = 500; // Output square size: 500x500
    canvas.width = outputSize;
    canvas.height = outputSize;

    const ctx = canvas.getContext("2d");
    if (!ctx) return;

    const img = imgRef.current;

    // Background fill
    ctx.fillStyle = "#ffffff";
    ctx.fillRect(0, 0, outputSize, outputSize);

    // Calculate crop dimensions
    const previewBoxSize = 280; // Size of preview container
    const scale = (outputSize / previewBoxSize) * zoom;

    const naturalWidth = img.naturalWidth;
    const naturalHeight = img.naturalHeight;

    // Center point
    const baseScale = Math.max(previewBoxSize / naturalWidth, previewBoxSize / naturalHeight);
    const scaledWidth = naturalWidth * baseScale * scale;
    const scaledHeight = naturalHeight * baseScale * scale;

    const drawX = (outputSize - scaledWidth) / 2 + pan.x * (outputSize / previewBoxSize);
    const drawY = (outputSize - scaledHeight) / 2 + pan.y * (outputSize / previewBoxSize);

    ctx.drawImage(img, drawX, drawY, scaledWidth, scaledHeight);

    canvas.toBlob(
      (blob) => {
        if (blob) {
          onConfirmCrop(blob);
        }
      },
      "image/webp",
      0.92
    );
  }, [zoom, pan, onConfirmCrop]);

  if (!imageSrc) return null;

  return (
    <Modal
      isOpen={isOpen}
      onClose={onClose}
      title="Adjust & Crop Avatar"
      description="Position and crop your avatar image to fit a 1:1 aspect ratio."
    >
      <div className="space-y-5">
        {/* Interactive Crop Viewport */}
        <div
          className="relative mx-auto h-[280px] w-[280px] cursor-grab select-none overflow-hidden rounded-full border-4 border-primary/30 bg-muted active:cursor-grabbing shadow-inner"
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
          onMouseLeave={handleMouseUp}
          onTouchStart={handleTouchStart}
          onTouchMove={handleTouchMove}
          onTouchEnd={handleTouchEnd}
        >
          {imageSrc && (
            <img
              src={imageSrc}
              alt="Crop target"
              draggable={false}
              style={{
                transform: `translate(${pan.x}px, ${pan.y}px) scale(${zoom})`,
                transformOrigin: "center center",
                transition: isDragging ? "none" : "transform 0.05s ease-out",
                maxWidth: "100%",
                maxHeight: "100%",
                objectFit: "cover",
                width: "100%",
                height: "100%",
              }}
              className="pointer-events-none"
            />
          )}
        </div>

        {/* Zoom Slider Controls */}
        <div className="space-y-2 px-2">
          <div className="flex items-center justify-between text-xs font-medium text-muted-foreground">
            <span className="flex items-center space-x-1">
              <ZoomOut className="h-3.5 w-3.5" />
              <span>Zoom</span>
            </span>
            <span>{Math.round(zoom * 100)}%</span>
          </div>
          <div className="flex items-center space-x-3">
            <input
              type="range"
              min="1"
              max="3"
              step="0.05"
              value={zoom}
              onChange={(e) => setZoom(parseFloat(e.target.value))}
              className="h-2 w-full cursor-pointer appearance-none rounded-lg bg-muted accent-primary"
            />
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={handleReset}
              title="Reset Zoom and Pan"
              className="h-8 px-2 text-xs"
            >
              <RotateCcw className="h-3.5 w-3.5" />
            </Button>
          </div>
        </div>

        {/* Action Buttons */}
        <div className="flex justify-end space-x-2 pt-3 border-t border-border">
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={onClose}
            disabled={isProcessing}
          >
            Cancel
          </Button>
          <Button
            type="button"
            variant="default"
            size="sm"
            onClick={handleApply}
            isLoading={isProcessing}
          >
            <Check className="h-3.5 w-3.5 mr-1.5" />
            Apply & Upload
          </Button>
        </div>
      </div>
    </Modal>
  );
}
