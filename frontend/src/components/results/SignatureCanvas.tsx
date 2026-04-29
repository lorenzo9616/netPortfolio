'use client';

import { useEffect, useRef, useState } from 'react';

interface Rect { x: number; y: number; w: number; h: number }

interface Props {
  imageUrl: string;
  onCapture: (base64Png: string) => void;
  isSaving: boolean;
  captureError: string | null;
  pageCount?: number;
  currentPage?: number;
  onPageChange?: (page: number) => void;
}

export default function SignatureCanvas({
  imageUrl,
  onCapture,
  isSaving,
  captureError,
  pageCount = 1,
  currentPage = 1,
  onPageChange,
}: Props) {
  const imgRef = useRef<HTMLImageElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [dragStart, setDragStart] = useState<{ x: number; y: number } | null>(null);
  const [selection, setSelection] = useState<Rect | null>(null);
  const [imageLoaded, setImageLoaded] = useState(false);
  const [imageError, setImageError] = useState(false);

  // Reset canvas state when navigating to a different page
  useEffect(() => {
    setSelection(null);
    setImageLoaded(false);
    setImageError(false);
  }, [currentPage]);

  // Sync canvas buffer size to displayed image size whenever the image loads
  useEffect(() => {
    const canvas = canvasRef.current;
    const img = imgRef.current;
    if (!canvas || !img || !imageLoaded) return;
    canvas.width = img.clientWidth;
    canvas.height = img.clientHeight;
  }, [imageLoaded]);

  // Redraw selection rectangle whenever selection changes
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    if (!selection) return;
    ctx.strokeStyle = '#ef4444';
    ctx.lineWidth = 2;
    ctx.setLineDash([6, 3]);
    ctx.strokeRect(selection.x, selection.y, selection.w, selection.h);
  }, [selection]);

  function getPos(e: React.MouseEvent<HTMLCanvasElement>): { x: number; y: number } {
    const rect = canvasRef.current!.getBoundingClientRect();
    return { x: e.clientX - rect.left, y: e.clientY - rect.top };
  }

  function toRect(a: { x: number; y: number }, b: { x: number; y: number }): Rect {
    return { x: Math.min(a.x, b.x), y: Math.min(a.y, b.y), w: Math.abs(b.x - a.x), h: Math.abs(b.y - a.y) };
  }

  function handleMouseDown(e: React.MouseEvent<HTMLCanvasElement>) {
    setDragStart(getPos(e));
    setSelection(null);
  }

  function handleMouseMove(e: React.MouseEvent<HTMLCanvasElement>) {
    if (!dragStart) return;
    setSelection(toRect(dragStart, getPos(e)));
  }

  function handleMouseUp(e: React.MouseEvent<HTMLCanvasElement>) {
    if (!dragStart) return;
    setSelection(toRect(dragStart, getPos(e)));
    setDragStart(null);
  }

  function handleCapture() {
    const img = imgRef.current;
    if (!img || !selection || selection.w <= 5 || selection.h <= 5) return;

    const scaleX = img.naturalWidth / img.clientWidth;
    const scaleY = img.naturalHeight / img.clientHeight;
    const nx = Math.round(selection.x * scaleX);
    const ny = Math.round(selection.y * scaleY);
    const nw = Math.round(selection.w * scaleX);
    const nh = Math.round(selection.h * scaleY);
    if (nw <= 0 || nh <= 0) return;

    const offscreen = document.createElement('canvas');
    offscreen.width = nw;
    offscreen.height = nh;
    const ctx = offscreen.getContext('2d');
    if (!ctx) return;
    ctx.drawImage(img, -nx, -ny, img.naturalWidth, img.naturalHeight);
    const base64 = offscreen.toDataURL('image/png').split(',')[1];
    onCapture(base64);
  }

  const hasValidSelection = selection !== null && selection.w > 5 && selection.h > 5;
  const showPageNav = pageCount > 1 && onPageChange !== undefined;

  if (imageError) {
    return (
      <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-5 text-center">
        <p className="text-xs text-gray-500">Image preview not available.</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-2">
        <p className="text-xs text-gray-500">
          Draw a box over the signature area, then click <strong>Capture Signature</strong>.
        </p>
        {showPageNav && (
          <div className="flex shrink-0 items-center gap-1">
            <button
              type="button"
              onClick={() => onPageChange(currentPage - 1)}
              disabled={currentPage <= 1}
              aria-label="Previous page"
              className="rounded px-2 py-1 text-xs font-medium text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-40"
            >
              ← Prev
            </button>
            <span className="text-xs text-gray-500">
              {currentPage}&nbsp;/&nbsp;{pageCount}
            </span>
            <button
              type="button"
              onClick={() => onPageChange(currentPage + 1)}
              disabled={currentPage >= pageCount}
              aria-label="Next page"
              className="rounded px-2 py-1 text-xs font-medium text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-40"
            >
              Next →
            </button>
          </div>
        )}
      </div>

      <div className="relative overflow-hidden rounded-lg border border-gray-200 bg-gray-50">
        {/* crossOrigin="anonymous" required so canvas.drawImage() can read cross-origin pixels */}
        <img
          ref={imgRef}
          src={imageUrl}
          alt={`Document preview — page ${currentPage}`}
          crossOrigin="anonymous"
          onLoad={() => setImageLoaded(true)}
          onError={() => setImageError(true)}
          className="w-full select-none"
          draggable={false}
        />
        {imageLoaded && (
          <canvas
            ref={canvasRef}
            onMouseDown={handleMouseDown}
            onMouseMove={handleMouseMove}
            onMouseUp={handleMouseUp}
            className="absolute inset-0 cursor-crosshair"
            style={{ width: '100%', height: '100%' }}
          />
        )}
      </div>

      {hasValidSelection && (
        <button
          type="button"
          onClick={handleCapture}
          disabled={isSaving}
          className="w-full rounded-xl bg-emerald-600 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-emerald-500 focus:ring-offset-2"
        >
          {isSaving ? 'Capturing…' : 'Capture Signature'}
        </button>
      )}

      {captureError && (
        <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-4 py-3">
          <p className="text-sm font-semibold text-red-800">Capture failed</p>
          <p className="mt-0.5 text-xs text-red-700">{captureError}</p>
        </div>
      )}
    </div>
  );
}
