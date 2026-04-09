'use client';

import { useEffect, useRef, useState } from 'react';

interface CameraCaptureProps {
  onCapture: (file: File) => void;
  onCancel: () => void;
}

export default function CameraCapture({ onCapture, onCancel }: CameraCaptureProps) {
  const videoRef = useRef<HTMLVideoElement>(null);
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const streamRef = useRef<MediaStream | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isReady, setIsReady] = useState(false);

  useEffect(() => {
    let cancelled = false;

    async function startCamera() {
      try {
        const stream = await navigator.mediaDevices.getUserMedia({
          video: { facingMode: 'environment' },
        });
        if (cancelled) {
          stream.getTracks().forEach((t) => t.stop());
          return;
        }
        streamRef.current = stream;
        if (videoRef.current) {
          videoRef.current.srcObject = stream;
          setIsReady(true);
        }
      } catch (err) {
        if (cancelled) return;
        if (err instanceof DOMException && err.name === 'NotAllowedError') {
          setError('Camera access denied. Please allow camera access in your browser settings.');
        } else if (err instanceof DOMException && err.name === 'NotFoundError') {
          setError('No camera found on this device.');
        } else {
          setError('Could not start camera. Please try again.');
        }
      }
    }

    startCamera();

    return () => {
      cancelled = true;
      // CRITICAL: stop all tracks to release camera hardware and prevent memory leaks
      if (streamRef.current) {
        streamRef.current.getTracks().forEach((t) => t.stop());
        streamRef.current = null;
      }
    };
  }, []);

  function stopStream() {
    if (streamRef.current) {
      streamRef.current.getTracks().forEach((t) => t.stop());
      streamRef.current = null;
    }
  }

  function handleCapture() {
    const video = videoRef.current;
    const canvas = canvasRef.current;
    if (!video || !canvas) return;

    canvas.width = video.videoWidth;
    canvas.height = video.videoHeight;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.drawImage(video, 0, 0);

    canvas.toBlob((blob) => {
      if (!blob) return;
      const file = new File([blob], `capture-${Date.now()}.jpg`, { type: 'image/jpeg' });
      stopStream();
      onCapture(file);
    }, 'image/jpeg', 0.92);
  }

  function handleCancel() {
    stopStream();
    onCancel();
  }

  return (
    <div className="flex flex-col gap-4">
      {error ? (
        <div
          role="alert"
          className="rounded-xl border border-red-200 bg-red-50 p-6 text-center text-sm text-red-700"
        >
          <svg
            aria-hidden="true"
            className="mx-auto mb-3 h-10 w-10 text-red-400"
            fill="none"
            stroke="currentColor"
            strokeWidth={1.5}
            viewBox="0 0 24 24"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126zM12 15.75h.007v.008H12v-.008z"
            />
          </svg>
          <p className="font-medium">{error}</p>
          <button
            type="button"
            aria-label="Go back without using camera"
            onClick={handleCancel}
            className="mt-4 rounded-lg bg-gray-100 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-200 focus:outline-none focus:ring-2 focus:ring-gray-400"
          >
            Go Back
          </button>
        </div>
      ) : (
        <>
          {/* Live video preview */}
          <div className="relative overflow-hidden rounded-xl bg-black">
            <video
              ref={videoRef}
              autoPlay
              playsInline
              muted
              aria-label="Camera preview"
              className="w-full max-w-full"
            />
            {!isReady && (
              <div className="absolute inset-0 flex items-center justify-center">
                <p className="text-sm text-gray-300">Starting camera...</p>
              </div>
            )}
          </div>

          {/* Hidden canvas used for frame capture */}
          <canvas ref={canvasRef} className="hidden" aria-hidden="true" />

          {/* Actions */}
          <div className="flex gap-3">
            <button
              type="button"
              aria-label="Capture photo from camera"
              disabled={!isReady}
              onClick={handleCapture}
              className="flex-1 rounded-lg bg-blue-600 px-4 py-2.5 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
            >
              Capture Photo
            </button>
            <button
              type="button"
              aria-label="Cancel camera capture and go back"
              onClick={handleCancel}
              className="flex-1 rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-400 focus:ring-offset-2"
            >
              Cancel
            </button>
          </div>
        </>
      )}
    </div>
  );
}
