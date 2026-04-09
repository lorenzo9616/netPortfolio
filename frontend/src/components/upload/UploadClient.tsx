'use client';

import { useReducer, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import type { UploadState, CropRegion } from '@/types/ocr';
import { analyzeDocument, ApiError } from '@/lib/api';
import FileDropzone from './FileDropzone';
import CameraCapture from './CameraCapture';
import CanvasPreview from './CanvasPreview';

// ─── Reducer ──────────────────────────────────────────────────────────────────

type Action =
  | { type: 'SET_FILE'; file: File; previewUrl: string }
  | { type: 'SET_CROP'; crop: CropRegion | null }
  | { type: 'SET_CAPTURING'; value: boolean }
  | { type: 'RESET' };

function reducer(state: UploadState, action: Action): UploadState {
  switch (action.type) {
    case 'SET_FILE':
      // Revoke previous preview URL to avoid memory leaks
      if (state.previewUrl) URL.revokeObjectURL(state.previewUrl);
      return {
        ...state,
        file: action.file,
        previewUrl: action.previewUrl,
        imageDimensions: null,
        cropRegion: null,
        isCapturing: false,
      };
    case 'SET_CROP':
      return { ...state, cropRegion: action.crop };
    case 'SET_CAPTURING':
      return { ...state, isCapturing: action.value };
    case 'RESET':
      if (state.previewUrl) URL.revokeObjectURL(state.previewUrl);
      return initialState;
    default:
      return state;
  }
}

const initialState: UploadState = {
  file: null,
  previewUrl: null,
  imageDimensions: null,
  cropRegion: null,
  isCapturing: false,
};

// ─── Analyze result types ─────────────────────────────────────────────────────

type AnalyzeStatus =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'error'; message: string };

// ─── Component ────────────────────────────────────────────────────────────────

export default function UploadClient() {
  const router = useRouter();
  const [state, dispatch] = useReducer(reducer, initialState);
  const [analyzeStatus, setAnalyzeStatus] = useReducerLike<AnalyzeStatus>({ status: 'idle' });

  function handleFileSelected(file: File) {
    const previewUrl = URL.createObjectURL(file);
    dispatch({ type: 'SET_FILE', file, previewUrl });
    setAnalyzeStatus({ status: 'idle' });
  }

  function handleCameraToggle() {
    dispatch({ type: 'SET_CAPTURING', value: true });
  }

  function handleCameraCapture(file: File) {
    const previewUrl = URL.createObjectURL(file);
    dispatch({ type: 'SET_FILE', file, previewUrl });
    setAnalyzeStatus({ status: 'idle' });
  }

  function handleCameraCancel() {
    dispatch({ type: 'SET_CAPTURING', value: false });
  }

  const handleCropChange = useCallback((crop: CropRegion | null) => {
    dispatch({ type: 'SET_CROP', crop });
  }, []);

  function handleReset() {
    dispatch({ type: 'RESET' });
    setAnalyzeStatus({ status: 'idle' });
  }

  async function handleAnalyze() {
    if (!state.file) return;
    setAnalyzeStatus({ status: 'loading' });
    try {
      const result = await analyzeDocument(state.file, state.cropRegion ?? undefined);
      // Redirect immediately — no intermediate success panel
      router.push(`/results/${result.documentId}`);
    } catch (err) {
      const message =
        err instanceof ApiError
          ? err.message
          : err instanceof Error
          ? err.message
          : 'An unexpected error occurred.';
      setAnalyzeStatus({ status: 'error', message });
    }
  }

  const canAnalyze = state.file !== null && analyzeStatus.status !== 'loading';

  return (
    <div className="flex flex-col gap-6">
      {/* Two-column layout: left = dropzone/camera, right = preview */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Left panel */}
        <section aria-label="File input">
          <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">
            Select Document
          </h2>

          {state.isCapturing ? (
            <CameraCapture onCapture={handleCameraCapture} onCancel={handleCameraCancel} />
          ) : (
            <FileDropzone onFileSelected={handleFileSelected} onCameraToggle={handleCameraToggle} />
          )}

          {/* Selected file badge */}
          {state.file && !state.isCapturing && (
            <div className="mt-4 flex items-center justify-between gap-3 rounded-lg border border-gray-200 bg-white px-4 py-3">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium text-gray-800">{state.file.name}</p>
                <p className="text-xs text-gray-400">
                  {(state.file.size / 1024 / 1024).toFixed(2)} MB
                </p>
              </div>
              <button
                type="button"
                aria-label="Remove selected file and start over"
                onClick={handleReset}
                className="shrink-0 rounded-md bg-gray-100 px-3 py-1 text-xs font-medium text-gray-600 hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-400"
              >
                Remove
              </button>
            </div>
          )}
        </section>

        {/* Right panel */}
        {state.file && !state.isCapturing && (
          <section aria-label="Document preview and crop selection">
            <h2 className="mb-3 text-sm font-semibold uppercase tracking-wide text-gray-500">
              Preview & Crop
            </h2>
            <CanvasPreview file={state.file} onCropChange={handleCropChange} />
          </section>
        )}
      </div>

      {/* Analyze button */}
      {!state.isCapturing && (
        <div className="flex flex-col gap-3">
          <button
            type="button"
            aria-label={
              canAnalyze
                ? state.cropRegion
                  ? 'Analyze the selected crop region'
                  : 'Analyze the full document'
                : 'Select a file before analyzing'
            }
            disabled={!canAnalyze}
            onClick={handleAnalyze}
            className="w-full rounded-xl bg-blue-600 px-6 py-3 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 sm:w-auto"
          >
            {analyzeStatus.status === 'loading' ? (
              <span className="flex items-center justify-center gap-2">
                <svg
                  aria-hidden="true"
                  className="h-4 w-4 animate-spin"
                  fill="none"
                  viewBox="0 0 24 24"
                >
                  <circle
                    className="opacity-25"
                    cx="12"
                    cy="12"
                    r="10"
                    stroke="currentColor"
                    strokeWidth="4"
                  />
                  <path
                    className="opacity-75"
                    fill="currentColor"
                    d="M4 12a8 8 0 018-8v8H4z"
                  />
                </svg>
                Analyzing document…
              </span>
            ) : (
              `Analyze Document${state.cropRegion ? ' (cropped)' : ''}`
            )}
          </button>

          {/* Analyze result feedback */}
          {analyzeStatus.status === 'error' && (
            <div
              role="alert"
              className="rounded-xl border border-red-200 bg-red-50 px-5 py-4"
            >
              <p className="text-sm font-semibold text-red-800">Analysis failed</p>
              <p className="mt-1 text-xs text-red-700">{analyzeStatus.message}</p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ─── Tiny useState-like helper (avoids useReducer boilerplate for flat state) ─

function useReducerLike<T>(init: T): [T, (v: T) => void] {
  const [value, dispatch] = useReducer((_: T, next: T) => next, init);
  return [value, dispatch];
}
