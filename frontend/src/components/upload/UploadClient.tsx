'use client';

import { useReducer, useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import type { UploadState, CropRegion } from '@/types/ocr';
import { analyzeDocumentStream, ApiError } from '@/lib/api';
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
  | { status: 'streaming'; steps: string[] }
  | { status: 'error'; message: string };

// ─── Component ────────────────────────────────────────────────────────────────

const LANGUAGES = [
  { code: 'eng', label: 'English' },
  { code: 'spa', label: 'Spanish' },
  { code: 'fra', label: 'French' },
  { code: 'deu', label: 'German' },
  { code: 'ita', label: 'Italian' },
  { code: 'por', label: 'Portuguese' },
  { code: 'chi_sim', label: 'Chinese (Simplified)' },
  { code: 'jpn', label: 'Japanese' },
  { code: 'kor', label: 'Korean' },
  { code: 'ara', label: 'Arabic' },
];

export default function UploadClient() {
  const router = useRouter();
  const [state, dispatch] = useReducer(reducer, initialState);
  const [analyzeStatus, setAnalyzeStatus] = useState<AnalyzeStatus>({ status: 'idle' });
  const [lang, setLang] = useState('eng');

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
    setAnalyzeStatus({ status: 'streaming', steps: [] });
    try {
      const result = await analyzeDocumentStream(
        state.file,
        state.cropRegion ?? undefined,
        lang,
        (message) => {
          setAnalyzeStatus((prev) =>
            prev.status === 'streaming'
              ? { status: 'streaming', steps: [...prev.steps, message] }
              : prev,
          );
        },
      );
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

  const canAnalyze = state.file !== null && analyzeStatus.status !== 'streaming';

  return (
    <div className="flex flex-col gap-5">
      {/* Two-column layout: left = dropzone/camera, right = preview */}
      <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
        {/* Left panel */}
        <section aria-label="File input">
          <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
            Select Document
          </h2>

          {state.isCapturing ? (
            <CameraCapture onCapture={handleCameraCapture} onCancel={handleCameraCancel} />
          ) : (
            <FileDropzone onFileSelected={handleFileSelected} onCameraToggle={handleCameraToggle} />
          )}

          {/* Selected file badge */}
          {state.file && !state.isCapturing && (
            <div className="mt-3 flex items-center gap-3 rounded-lg border border-gray-200 bg-white px-3 py-2.5">
              <svg aria-hidden="true" className="h-4 w-4 shrink-0 text-gray-400" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
              </svg>
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-gray-800">{state.file.name}</p>
                <p className="text-xs text-gray-400">{(state.file.size / 1024 / 1024).toFixed(2)} MB</p>
              </div>
              <button
                type="button"
                aria-label="Remove selected file and start over"
                onClick={handleReset}
                className="shrink-0 rounded-md px-2.5 py-1 text-xs font-medium text-gray-500 hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-400"
              >
                Remove
              </button>
            </div>
          )}
        </section>

        {/* Right panel */}
        {state.file && !state.isCapturing && (
          <section aria-label="Document preview and crop selection">
            <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
              Preview & Crop
            </h2>
            <CanvasPreview file={state.file} onCropChange={handleCropChange} />
          </section>
        )}
      </div>

      {/* Language + Analyze row */}
      {!state.isCapturing && (
        <div className="flex flex-wrap items-end gap-3">
          {/* Language selector */}
          <div className="flex flex-col gap-1">
            <label htmlFor="ocr-lang" className="text-xs font-medium text-gray-500">
              Language
            </label>
            <select
              id="ocr-lang"
              value={lang}
              onChange={(e) => setLang(e.target.value)}
              className="rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500"
            >
              {LANGUAGES.map((l) => (
                <option key={l.code} value={l.code}>
                  {l.label}
                </option>
              ))}
            </select>
          </div>

          {/* Analyze button */}
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
            className="rounded-xl bg-blue-600 px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
          >
            {analyzeStatus.status === 'streaming' ? (
              <span className="flex items-center gap-2">
                <svg aria-hidden="true" className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                  <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                  <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                </svg>
                Analyzing…
              </span>
            ) : (
              `Analyze${state.cropRegion ? ' (cropped)' : ''}`
            )}
          </button>
        </div>
      )}

      {/* Real-time progress steps */}
      {!state.isCapturing && analyzeStatus.status === 'streaming' && analyzeStatus.steps.length > 0 && (
        <div className="rounded-xl border border-blue-100 bg-blue-50 px-4 py-3">
          <ul className="flex flex-col gap-1.5">
            {analyzeStatus.steps.map((step, i) => (
              <li key={i} className="flex items-center gap-2 text-sm text-blue-800">
                <svg className="h-4 w-4 shrink-0 text-blue-500" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                  <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                </svg>
                {step}
              </li>
            ))}
            <li className="flex items-center gap-2 text-sm text-blue-500">
              <svg aria-hidden="true" className="h-4 w-4 shrink-0 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
              </svg>
              Processing…
            </li>
          </ul>
        </div>
      )}

      {/* Error feedback */}
      {!state.isCapturing && analyzeStatus.status === 'error' && (
        <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-4 py-3">
          <p className="text-sm font-semibold text-red-800">Analysis failed</p>
          <p className="mt-0.5 text-xs text-red-700">{analyzeStatus.message}</p>
        </div>
      )}
    </div>
  );
}
