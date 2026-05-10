'use client';

import { useReducer, useState, useCallback, useRef } from 'react';
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

type UploadTab = 'single' | 'batch';

interface QueuedFile {
  id: string;
  file: File;
  status: 'idle' | 'streaming' | 'done' | 'error';
  steps: string[];
  documentId?: number;
  errorMessage?: string;
}

export default function UploadClient() {
  const router = useRouter();
  const [state, dispatch] = useReducer(reducer, initialState);
  const [analyzeStatus, setAnalyzeStatus] = useState<AnalyzeStatus>({ status: 'idle' });
  const [lang, setLang] = useState('eng');
  const [tab, setTab]             = useState<UploadTab>('single');
  const [handwriting, setHandwriting] = useState(false);
  const [queuedFiles, setQueuedFiles] = useState<QueuedFile[]>([]);
  const [batchRunning, setBatchRunning] = useState(false);
  const batchFileInputRef = useRef<HTMLInputElement>(null);

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
        handwriting ? 'handwriting' : 'ocr',
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

  function handleBatchFilesSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const files = Array.from(e.target.files ?? []);
    if (files.length === 0) return;
    const newEntries: QueuedFile[] = files.map((file) => ({
      id: `${file.name}-${file.size}-${Date.now()}-${Math.random()}`,
      file,
      status: 'idle',
      steps: [],
    }));
    setQueuedFiles((prev) => [...prev, ...newEntries]);
    if (batchFileInputRef.current) batchFileInputRef.current.value = '';
  }

  function handleRemoveQueuedFile(id: string) {
    setQueuedFiles((prev) => prev.filter((f) => f.id !== id));
  }

  async function handleBatchAnalyze() {
    const pending = queuedFiles.filter((f) => f.status === 'idle');
    if (pending.length === 0 || batchRunning) return;
    setBatchRunning(true);
    for (const qf of pending) {
      setQueuedFiles((prev) =>
        prev.map((f) => (f.id === qf.id ? { ...f, status: 'streaming', steps: [] } : f)),
      );
      try {
        const result = await analyzeDocumentStream(
          qf.file,
          undefined,
          lang,
          handwriting ? 'handwriting' : 'ocr',
          (message) => {
            setQueuedFiles((prev) =>
              prev.map((f) =>
                f.id === qf.id ? { ...f, steps: [...f.steps, message] } : f,
              ),
            );
          },
        );
        setQueuedFiles((prev) =>
          prev.map((f) =>
            f.id === qf.id ? { ...f, status: 'done', documentId: result.documentId } : f,
          ),
        );
      } catch (err) {
        const message =
          err instanceof ApiError
            ? err.message
            : err instanceof Error
            ? err.message
            : 'An unexpected error occurred.';
        setQueuedFiles((prev) =>
          prev.map((f) =>
            f.id === qf.id ? { ...f, status: 'error', errorMessage: message } : f,
          ),
        );
      }
    }
    setBatchRunning(false);
  }

  const idleCount     = queuedFiles.filter((f) => f.status === 'idle').length;
  const canBatchStart = idleCount > 0 && !batchRunning;

  return (
    <div className="flex flex-col gap-5">
      {/* ── Tab bar ────────────────────────────────────────────────────────── */}
      <div className="flex gap-0.5 self-start rounded-xl border border-gray-200 bg-gray-50 p-0.5">
        {(['single', 'batch'] as UploadTab[]).map((t) => (
          <button
            key={t}
            type="button"
            onClick={() => setTab(t)}
            className={`rounded-lg px-4 py-1.5 text-sm font-semibold transition-colors ${
              tab === t
                ? 'bg-white text-gray-800 shadow-sm'
                : 'text-gray-500 hover:text-gray-700'
            }`}
          >
            {t === 'single' ? 'Single Document' : 'Batch Upload'}
          </button>
        ))}
      </div>

      {/* ── Handwriting toggle ─────────────────────────────────────────────── */}
      <label className="flex cursor-pointer items-center gap-3 self-start rounded-xl border border-gray-200 bg-white px-4 py-2.5">
        <input
          type="checkbox"
          checked={handwriting}
          onChange={(e) => setHandwriting(e.target.checked)}
          className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <span className="text-sm font-medium text-gray-700">
          Handwriting / ICR mode
        </span>
        <span className="text-xs text-gray-400">uses Claude Vision</span>
      </label>

      {/* ── Single tab ─────────────────────────────────────────────────────── */}
      {tab === 'single' && (
        <>
          <div className="grid grid-cols-1 gap-5 lg:grid-cols-2">
            <section aria-label="File input">
              <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
                Select Document
              </h2>
              {state.isCapturing ? (
                <CameraCapture onCapture={handleCameraCapture} onCancel={handleCameraCancel} />
              ) : (
                <FileDropzone onFileSelected={handleFileSelected} onCameraToggle={handleCameraToggle} />
              )}
              {state.file && !state.isCapturing && (
                <div className="mt-3 flex items-center gap-3 rounded-lg border border-gray-200 bg-white px-3 py-2.5">
                  <svg aria-hidden="true" className="h-4 w-4 shrink-0 text-gray-400" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" d="M19.5 14.25v-2.625a3.375 3.375 0 00-3.375-3.375h-1.5A1.125 1.125 0 0113.5 7.125v-1.5a3.375 3.375 0 00-3.375-3.375H8.25m2.25 0H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 00-9-9z" />
                  </svg>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-gray-800">{state.file.name}</p>
                    <p className="text-xs text-gray-400">{(state.file.size / 1024 / 1024).toFixed(2)} MB</p>
                  </div>
                  <button type="button" aria-label="Remove file" onClick={handleReset}
                    className="shrink-0 rounded-md px-2.5 py-1 text-xs font-medium text-gray-500 hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-400">
                    Remove
                  </button>
                </div>
              )}
            </section>
            {state.file && !state.isCapturing && (
              <section aria-label="Document preview and crop selection">
                <h2 className="mb-3 text-xs font-semibold uppercase tracking-wide text-gray-500">
                  Preview & Crop
                </h2>
                <CanvasPreview file={state.file} onCropChange={handleCropChange} />
              </section>
            )}
          </div>

          {!state.isCapturing && (
            <div className="flex flex-wrap items-end gap-3">
              {!handwriting && (
                <div className="flex flex-col gap-1">
                  <label htmlFor="ocr-lang" className="text-xs font-medium text-gray-500">Language</label>
                  <select id="ocr-lang" value={lang} onChange={(e) => setLang(e.target.value)}
                    className="rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500">
                    {LANGUAGES.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}
                  </select>
                </div>
              )}
              <button type="button"
                aria-label={state.file ? 'Analyze document' : 'Select a file before analyzing'}
                disabled={!state.file || analyzeStatus.status === 'streaming'}
                onClick={handleAnalyze}
                className="rounded-xl bg-blue-600 px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2">
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

          {!state.isCapturing && analyzeStatus.status === 'error' && (
            <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-4 py-3">
              <p className="text-sm font-semibold text-red-800">Analysis failed</p>
              <p className="mt-0.5 text-xs text-red-700">{analyzeStatus.message}</p>
            </div>
          )}
        </>
      )}

      {/* ── Batch tab ──────────────────────────────────────────────────────── */}
      {tab === 'batch' && (
        <>
          {/* File picker */}
          <div>
            <input
              ref={batchFileInputRef}
              type="file"
              id="batch-file-input"
              multiple
              accept=".pdf,.png,.jpg,.jpeg,image/png,image/jpeg,application/pdf"
              className="hidden"
              onChange={handleBatchFilesSelected}
            />
            <label
              htmlFor="batch-file-input"
              className="flex cursor-pointer flex-col items-center justify-center gap-2 rounded-xl border-2 border-dashed border-gray-300 bg-gray-50 px-6 py-8 text-center transition-colors hover:border-blue-400 hover:bg-blue-50"
            >
              <svg className="h-8 w-8 text-gray-400" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24" aria-hidden="true">
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 16.5V9.75m0 0l-3 3m3-3l3 3M6.75 19.5a4.5 4.5 0 01-1.41-8.775 5.25 5.25 0 0110.233-2.33 3 3 0 013.758 3.848A3.752 3.752 0 0118 19.5H6.75z" />
              </svg>
              <span className="text-sm font-semibold text-gray-700">Click to browse files</span>
              <span className="text-xs text-gray-400">PDF, PNG, JPEG — multiple files allowed</span>
            </label>
          </div>

          {/* Queue list */}
          {queuedFiles.length > 0 && (
            <div className="flex flex-col gap-2">
              {queuedFiles.map((qf) => (
                <div key={qf.id} className="rounded-xl border border-gray-200 bg-white px-4 py-3">
                  <div className="flex items-center gap-3">
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium text-gray-800">{qf.file.name}</p>
                      <p className="text-xs text-gray-400">{(qf.file.size / 1024 / 1024).toFixed(2)} MB</p>
                    </div>
                    {qf.status === 'idle' && (
                      <span className="text-xs font-medium text-gray-400">Waiting</span>
                    )}
                    {qf.status === 'streaming' && (
                      <svg aria-hidden="true" className="h-4 w-4 shrink-0 animate-spin text-blue-500" fill="none" viewBox="0 0 24 24">
                        <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                        <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                      </svg>
                    )}
                    {qf.status === 'done' && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-green-100 px-2 py-0.5 text-xs font-semibold text-green-800 ring-1 ring-inset ring-green-200">
                        <svg className="h-3 w-3" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                          <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                        </svg>
                        Done
                      </span>
                    )}
                    {qf.status === 'error' && (
                      <span className="rounded-full bg-red-100 px-2 py-0.5 text-xs font-semibold text-red-800 ring-1 ring-inset ring-red-200">
                        Error
                      </span>
                    )}
                    {qf.status === 'idle' && !batchRunning && (
                      <button
                        type="button"
                        aria-label={`Remove ${qf.file.name} from queue`}
                        onClick={() => handleRemoveQueuedFile(qf.id)}
                        className="shrink-0 rounded px-2 py-0.5 text-xs text-gray-400 hover:text-red-500"
                      >
                        ✕
                      </button>
                    )}
                  </div>

                  {/* SSE steps while streaming */}
                  {qf.status === 'streaming' && qf.steps.length > 0 && (
                    <ul className="mt-2 flex flex-col gap-1 pl-1">
                      {qf.steps.map((step, i) => (
                        <li key={i} className="flex items-center gap-1.5 text-xs text-blue-700">
                          <svg className="h-3 w-3 shrink-0 text-blue-400" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                            <path fillRule="evenodd" d="M16.707 5.293a1 1 0 010 1.414l-8 8a1 1 0 01-1.414 0l-4-4a1 1 0 011.414-1.414L8 12.586l7.293-7.293a1 1 0 011.414 0z" clipRule="evenodd" />
                          </svg>
                          {step}
                        </li>
                      ))}
                    </ul>
                  )}

                  {/* Done: link to results */}
                  {qf.status === 'done' && qf.documentId !== undefined && (
                    <a
                      href={`/results/${qf.documentId}`}
                      className="mt-1 block text-xs font-medium text-blue-600 hover:text-blue-800"
                    >
                      View Results →
                    </a>
                  )}

                  {/* Error message */}
                  {qf.status === 'error' && qf.errorMessage && (
                    <p className="mt-1 text-xs text-red-600">{qf.errorMessage}</p>
                  )}
                </div>
              ))}
            </div>
          )}

          {/* Language + Analyze row */}
          <div className="flex flex-wrap items-end gap-3">
            {!handwriting && (
              <div className="flex flex-col gap-1">
                <label htmlFor="batch-ocr-lang" className="text-xs font-medium text-gray-500">Language</label>
                <select id="batch-ocr-lang" value={lang} onChange={(e) => setLang(e.target.value)}
                  className="rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm text-gray-800 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500">
                  {LANGUAGES.map((l) => <option key={l.code} value={l.code}>{l.label}</option>)}
                </select>
              </div>
            )}
            <button
              type="button"
              disabled={!canBatchStart}
              onClick={handleBatchAnalyze}
              className="rounded-xl bg-blue-600 px-5 py-2 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
            >
              {batchRunning ? (
                <span className="flex items-center gap-2">
                  <svg aria-hidden="true" className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                    <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                    <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                  </svg>
                  Analyzing…
                </span>
              ) : (
                `Analyze ${idleCount > 0 ? idleCount : ''} Document${idleCount !== 1 ? 's' : ''}`
              )}
            </button>
          </div>
        </>
      )}
    </div>
  );
}
