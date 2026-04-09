'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import type { CropRegion } from '@/types/ocr';

interface CanvasPreviewProps {
  file: File;
  onCropChange: (crop: CropRegion | null) => void;
}

interface DrawPoint {
  x: number;
  y: number;
}

const MAX_CANVAS_WIDTH = 600;

export default function CanvasPreview({ file, onCropChange }: CanvasPreviewProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const imgRef = useRef<HTMLImageElement | null>(null);
  const isDragging = useRef(false);
  const dragStart = useRef<DrawPoint | null>(null);
  const currentRect = useRef<{ x: number; y: number; w: number; h: number } | null>(null);

  const [crop, setCrop] = useState<CropRegion | null>(null);
  const [isPdf, setIsPdf] = useState(false);
  // natural dimensions of the loaded image
  const naturalSize = useRef<{ width: number; height: number }>({ width: 1, height: 1 });

  // ─── Draw helpers ──────────────────────────────────────────────────────────

  const drawImage = useCallback(() => {
    const canvas = canvasRef.current;
    const img = imgRef.current;
    if (!canvas || !img) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
  }, []);

  const drawRect = useCallback(
    (rect: { x: number; y: number; w: number; h: number }) => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      if (!ctx) return;
      drawImage();
      ctx.save();
      ctx.fillStyle = 'rgba(59, 130, 246, 0.15)';
      ctx.strokeStyle = 'rgba(59, 130, 246, 0.9)';
      ctx.lineWidth = 2;
      ctx.fillRect(rect.x, rect.y, rect.w, rect.h);
      ctx.strokeRect(rect.x, rect.y, rect.w, rect.h);
      ctx.restore();
    },
    [drawImage],
  );

  // ─── Convert canvas rect → original image CropRegion ─────────────────────

  function rectToCrop(rect: { x: number; y: number; w: number; h: number }): CropRegion {
    const canvas = canvasRef.current!;
    const nw = naturalSize.current.width;
    const nh = naturalSize.current.height;
    const scaleX = nw / canvas.width;
    const scaleY = nh / canvas.height;

    // Normalize in case user dragged right-to-left or bottom-to-top
    const nx = rect.w >= 0 ? rect.x : rect.x + rect.w;
    const ny = rect.h >= 0 ? rect.y : rect.y + rect.h;
    const nw2 = Math.abs(rect.w);
    const nh2 = Math.abs(rect.h);

    return {
      x: Math.round(nx * scaleX),
      y: Math.round(ny * scaleY),
      width: Math.round(nw2 * scaleX),
      height: Math.round(nh2 * scaleY),
    };
  }

  // ─── Load file into image ──────────────────────────────────────────────────

  useEffect(() => {
    if (file.type === 'application/pdf' || file.name.toLowerCase().endsWith('.pdf')) {
      setIsPdf(true);
      setCrop(null);
      onCropChange(null);
      return;
    }

    setIsPdf(false);
    const url = URL.createObjectURL(file);
    const img = new Image();
    img.onload = () => {
      imgRef.current = img;
      naturalSize.current = { width: img.naturalWidth, height: img.naturalHeight };

      const canvas = canvasRef.current;
      if (!canvas) return;

      // Scale canvas to fit container (max 600px wide)
      const displayWidth = Math.min(img.naturalWidth, MAX_CANVAS_WIDTH);
      const displayHeight = Math.round((img.naturalHeight / img.naturalWidth) * displayWidth);
      canvas.width = displayWidth;
      canvas.height = displayHeight;

      drawImage();
    };
    img.src = url;

    return () => {
      URL.revokeObjectURL(url);
    };
  }, [file, drawImage, onCropChange]);

  // ─── Pointer coordinate helpers ───────────────────────────────────────────

  function getCanvasPoint(
    e: React.MouseEvent<HTMLCanvasElement> | React.TouchEvent<HTMLCanvasElement>,
  ): DrawPoint {
    const canvas = canvasRef.current!;
    const rect = canvas.getBoundingClientRect();
    // Actual canvas logical size vs. displayed CSS size
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;

    if ('touches' in e) {
      const touch = e.touches[0] ?? e.changedTouches[0];
      return {
        x: (touch.clientX - rect.left) * scaleX,
        y: (touch.clientY - rect.top) * scaleY,
      };
    }
    return {
      x: (e.clientX - rect.left) * scaleX,
      y: (e.clientY - rect.top) * scaleY,
    };
  }

  // ─── Mouse events ──────────────────────────────────────────────────────────

  function handleMouseDown(e: React.MouseEvent<HTMLCanvasElement>) {
    isDragging.current = true;
    dragStart.current = getCanvasPoint(e);
    currentRect.current = null;
    // Clear existing crop visually
    drawImage();
  }

  function handleMouseMove(e: React.MouseEvent<HTMLCanvasElement>) {
    if (!isDragging.current || !dragStart.current) return;
    const pt = getCanvasPoint(e);
    const rect = {
      x: dragStart.current.x,
      y: dragStart.current.y,
      w: pt.x - dragStart.current.x,
      h: pt.y - dragStart.current.y,
    };
    currentRect.current = rect;
    drawRect(rect);
  }

  function handleMouseUp(e: React.MouseEvent<HTMLCanvasElement>) {
    if (!isDragging.current || !dragStart.current) return;
    isDragging.current = false;
    const pt = getCanvasPoint(e);
    const rect = {
      x: dragStart.current.x,
      y: dragStart.current.y,
      w: pt.x - dragStart.current.x,
      h: pt.y - dragStart.current.y,
    };
    // Ignore tiny clicks (< 5px)
    if (Math.abs(rect.w) < 5 || Math.abs(rect.h) < 5) {
      drawImage();
      return;
    }
    currentRect.current = rect;
    drawRect(rect);
    const newCrop = rectToCrop(rect);
    setCrop(newCrop);
    onCropChange(newCrop);
  }

  // ─── Touch events ──────────────────────────────────────────────────────────

  function handleTouchStart(e: React.TouchEvent<HTMLCanvasElement>) {
    e.preventDefault();
    isDragging.current = true;
    dragStart.current = getCanvasPoint(e);
    currentRect.current = null;
    drawImage();
  }

  function handleTouchMove(e: React.TouchEvent<HTMLCanvasElement>) {
    e.preventDefault();
    if (!isDragging.current || !dragStart.current) return;
    const pt = getCanvasPoint(e);
    const rect = {
      x: dragStart.current.x,
      y: dragStart.current.y,
      w: pt.x - dragStart.current.x,
      h: pt.y - dragStart.current.y,
    };
    currentRect.current = rect;
    drawRect(rect);
  }

  function handleTouchEnd(e: React.TouchEvent<HTMLCanvasElement>) {
    e.preventDefault();
    if (!isDragging.current || !dragStart.current) return;
    isDragging.current = false;
    const pt = getCanvasPoint(e);
    const rect = {
      x: dragStart.current.x,
      y: dragStart.current.y,
      w: pt.x - dragStart.current.x,
      h: pt.y - dragStart.current.y,
    };
    if (Math.abs(rect.w) < 5 || Math.abs(rect.h) < 5) {
      drawImage();
      return;
    }
    currentRect.current = rect;
    drawRect(rect);
    const newCrop = rectToCrop(rect);
    setCrop(newCrop);
    onCropChange(newCrop);
  }

  function handleClearSelection() {
    setCrop(null);
    onCropChange(null);
    currentRect.current = null;
    drawImage();
  }

  // ─── PDF placeholder ────────────────────────────────────────────────────────

  if (isPdf) {
    return (
      <div className="flex flex-col gap-3">
        <div className="flex flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed border-gray-300 bg-gray-50 p-12 text-center text-gray-500">
          <svg
            aria-hidden="true"
            className="h-16 w-16 text-red-400"
            fill="none"
            stroke="currentColor"
            strokeWidth={1.5}
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z"
            />
          </svg>
          <p className="text-lg font-semibold text-gray-700">PDF Document</p>
          <p className="text-sm text-gray-400">{file.name}</p>
          <p className="text-xs text-gray-400">
            PDF preview not available in browser — the server will process the full document.
          </p>
        </div>
        <p className="text-xs text-gray-400">
          Cropping is not available for PDF files.
        </p>
      </div>
    );
  }

  // ─── Image canvas ───────────────────────────────────────────────────────────

  return (
    <div className="flex flex-col gap-3">
      <p className="text-xs text-gray-500">
        Draw a rectangle on the image to select a crop region, or analyze the full document.
      </p>

      {/* Canvas wrapper — percentage width for mobile responsiveness */}
      <div className="w-full max-w-full overflow-hidden rounded-xl border border-gray-200 bg-gray-100">
        <canvas
          ref={canvasRef}
          aria-label="Document preview. Draw a rectangle to select a crop region."
          className="w-full cursor-crosshair touch-none"
          onMouseDown={handleMouseDown}
          onMouseMove={handleMouseMove}
          onMouseUp={handleMouseUp}
          onMouseLeave={handleMouseUp}
          onTouchStart={handleTouchStart}
          onTouchMove={handleTouchMove}
          onTouchEnd={handleTouchEnd}
        />
      </div>

      {/* Crop info */}
      {crop ? (
        <div className="flex items-start justify-between gap-4 rounded-lg border border-blue-200 bg-blue-50 px-4 py-3">
          <div>
            <p className="text-xs font-semibold text-blue-700">Crop region (original pixels)</p>
            <p className="mt-0.5 font-mono text-xs text-blue-600">
              x:{crop.x} y:{crop.y} w:{crop.width} h:{crop.height}
            </p>
          </div>
          <button
            type="button"
            aria-label="Clear the current crop selection"
            onClick={handleClearSelection}
            className="shrink-0 rounded-md bg-blue-100 px-3 py-1 text-xs font-medium text-blue-700 transition-colors hover:bg-blue-200 focus:outline-none focus:ring-2 focus:ring-blue-500"
          >
            Clear Selection
          </button>
        </div>
      ) : (
        <p className="text-xs text-gray-400">No crop selected — full document will be analyzed.</p>
      )}
    </div>
  );
}
