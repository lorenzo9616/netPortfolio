'use client';

import { useEffect, useRef, useState } from 'react';
import type { OcrTextBlock } from '@/types/ocr';

interface TooltipState { x: number; y: number; text: string; confidence: number }

interface Props {
  imageUrl: string;
  blocks: OcrTextBlock[];
  currentPage: number;
  pageCount: number;
  onPageChange: (page: number) => void;
}

export default function BoundingBoxOverlay({
  imageUrl,
  blocks,
  currentPage,
  pageCount,
  onPageChange,
}: Props) {
  const imgRef    = useRef<HTMLImageElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [imageLoaded, setImageLoaded] = useState(false);
  const [imageError, setImageError]   = useState(false);
  const [tooltip, setTooltip]         = useState<TooltipState | null>(null);

  const pageBlocks = blocks.filter((b) => b.page === currentPage);

  useEffect(() => {
    setImageLoaded(false);
    setImageError(false);
    setTooltip(null);
  }, [currentPage, imageUrl]);

  useEffect(() => {
    const canvas = canvasRef.current;
    const img    = imgRef.current;
    if (!canvas || !img || !imageLoaded) return;

    canvas.width  = img.clientWidth;
    canvas.height = img.clientHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    const scaleX = img.clientWidth  / img.naturalWidth;
    const scaleY = img.clientHeight / img.naturalHeight;

    ctx.clearRect(0, 0, canvas.width, canvas.height);
    for (const block of pageBlocks) {
      const x = block.bboxX      * scaleX;
      const y = block.bboxY      * scaleY;
      const w = block.bboxWidth  * scaleX;
      const h = block.bboxHeight * scaleY;
      const pct = block.confidence;
      const [fill, stroke] =
        pct > 80
          ? ['rgba(34,197,94,0.20)',  'rgba(34,197,94,0.80)']
          : pct > 50
          ? ['rgba(234,179,8,0.20)',  'rgba(234,179,8,0.80)']
          : ['rgba(239,68,68,0.20)',  'rgba(239,68,68,0.80)'];
      ctx.fillStyle   = fill;
      ctx.fillRect(x, y, w, h);
      ctx.strokeStyle = stroke;
      ctx.lineWidth   = 1;
      ctx.setLineDash([]);
      ctx.strokeRect(x, y, w, h);
    }
  }, [imageLoaded, pageBlocks]);

  function handleMouseMove(e: React.MouseEvent<HTMLCanvasElement>) {
    const canvas = canvasRef.current;
    const img    = imgRef.current;
    if (!canvas || !img) return;

    const rect   = canvas.getBoundingClientRect();
    const mx     = e.clientX - rect.left;
    const my     = e.clientY - rect.top;
    const scaleX = img.clientWidth  / img.naturalWidth;
    const scaleY = img.clientHeight / img.naturalHeight;

    for (const block of pageBlocks) {
      const x = block.bboxX      * scaleX;
      const y = block.bboxY      * scaleY;
      const w = block.bboxWidth  * scaleX;
      const h = block.bboxHeight * scaleY;
      if (mx >= x && mx <= x + w && my >= y && my <= y + h) {
        setTooltip({ x: mx, y: my, text: block.text, confidence: Math.round(block.confidence) });
        return;
      }
    }
    setTooltip(null);
  }

  const showPageNav = pageCount > 1;

  if (imageError) {
    return (
      <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-5 text-center">
        <p className="text-xs text-gray-500">Image preview not available.</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-2">
      {showPageNav && (
        <div className="flex items-center justify-end gap-1">
          <button
            type="button"
            onClick={() => onPageChange(currentPage - 1)}
            disabled={currentPage <= 1}
            aria-label="Previous page"
            className="rounded px-2 py-1 text-xs font-medium text-gray-600 hover:bg-gray-100 disabled:cursor-not-allowed disabled:opacity-40"
          >
            ← Prev
          </button>
          <span className="text-xs text-gray-500">{currentPage}&nbsp;/&nbsp;{pageCount}</span>
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

      <div className="relative overflow-hidden rounded-lg border border-gray-200 bg-gray-50">
        <img
          ref={imgRef}
          src={imageUrl}
          alt={`Document page ${currentPage} — word overlay`}
          onLoad={() => setImageLoaded(true)}
          onError={() => setImageError(true)}
          className="w-full select-none"
          draggable={false}
        />
        {imageLoaded && (
          <canvas
            ref={canvasRef}
            onMouseMove={handleMouseMove}
            onMouseLeave={() => setTooltip(null)}
            className="absolute inset-0"
            style={{ width: '100%', height: '100%', cursor: 'default' }}
          />
        )}
        {tooltip && (
          <div
            className="pointer-events-none absolute z-10 max-w-xs rounded bg-gray-900 px-2 py-1 text-xs text-white shadow"
            style={{ left: tooltip.x + 10, top: Math.max(0, tooltip.y - 28) }}
          >
            {tooltip.text} — {tooltip.confidence}%
          </div>
        )}
      </div>

      {pageBlocks.length > 0 && (
        <div className="flex gap-3 text-xs text-gray-400">
          <span className="flex items-center gap-1">
            <span className="inline-block h-2 w-2 rounded-sm bg-green-400" /> &gt;80%
          </span>
          <span className="flex items-center gap-1">
            <span className="inline-block h-2 w-2 rounded-sm bg-yellow-400" /> 50–80%
          </span>
          <span className="flex items-center gap-1">
            <span className="inline-block h-2 w-2 rounded-sm bg-red-400" /> &lt;50%
          </span>
          <span className="ml-auto">{pageBlocks.length} words</span>
        </div>
      )}
    </div>
  );
}
