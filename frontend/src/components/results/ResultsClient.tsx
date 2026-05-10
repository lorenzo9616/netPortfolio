'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import type { AnalysisResultDetail, SavedFieldUpdate, SignatureDto } from '@/types/ocr';
import { saveFieldOverrides, addSignature, deleteSignature, ApiError } from '@/lib/api';
import SignatureCanvas from './SignatureCanvas';
import OcrTokensTable from './OcrTokensTable';
import BoundingBoxOverlay from './BoundingBoxOverlay';
import TableBlocksView from './TableBlocksView';
import ExtractedFieldsTable from './ExtractedFieldsTable';

interface Props {
  result: AnalysisResultDetail;
}

export default function ResultsClient({ result }: Props) {
  const [overrides, setOverrides] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {};
    for (const field of result.extractedFields) {
      initial[field.propertyName] = field.extractedValue ?? '';
    }
    return initial;
  });

  const [isSaving, setIsSaving]       = useState(false);
  const [saveStatus, setSaveStatus]   = useState<'idle' | 'success' | 'error'>('idle');
  const [saveError, setSaveError]     = useState('');
  const dismissTimerRef               = useRef<ReturnType<typeof setTimeout> | null>(null);

  const [signatures, setSignatures] = useState<SignatureDto[]>(result.signatures ?? []);
  const [isCapturing, setIsCapturing]   = useState(false);
  const [captureError, setCaptureError] = useState<string | null>(null);

  const [currentPage, setCurrentPage]     = useState(1);
  const [previewMode, setPreviewMode]     = useState<'overlay' | 'capture'>('overlay');

  const [imageUrl, setImageUrl] = useState<string | null>(null);
  useEffect(() => {
    const base = process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000';
    if (result.pageCount > 0) {
      setImageUrl(`${base}/api/documents/${result.documentId}/image/${currentPage}`);
    } else {
      setImageUrl(`${base}/api/documents/${result.documentId}/image`);
    }
  }, [result.documentId, result.pageCount, currentPage]);

  useEffect(() => {
    if (saveStatus !== 'success') return;
    dismissTimerRef.current = setTimeout(() => setSaveStatus('idle'), 3000);
    return () => {
      if (dismissTimerRef.current !== null) clearTimeout(dismissTimerRef.current);
    };
  }, [saveStatus]);

  function handleOverrideChange(propertyName: string, value: string) {
    setOverrides((prev) => ({ ...prev, [propertyName]: value }));
  }

  async function handleSave() {
    setIsSaving(true);
    setSaveStatus('idle');
    setSaveError('');
    const payload: SavedFieldUpdate[] = result.extractedFields.map((f) => ({
      propertyName:   f.propertyName,
      manualOverride: overrides[f.propertyName] ?? null,
    }));
    try {
      await saveFieldOverrides(result.documentId, payload);
      setSaveStatus('success');
    } catch (err) {
      const message =
        err instanceof ApiError ? err.message
        : err instanceof Error  ? err.message
        : 'An unexpected error occurred while saving.';
      setSaveError(message);
      setSaveStatus('error');
    } finally {
      setIsSaving(false);
    }
  }

  async function handleCapture(base64Png: string) {
    setIsCapturing(true);
    setCaptureError(null);
    try {
      const sig = await addSignature(result.documentId, base64Png, 'Manual capture');
      setSignatures((prev) => [...prev, sig]);
    } catch (err) {
      setCaptureError(
        err instanceof ApiError ? err.message
        : err instanceof Error  ? err.message
        : 'Capture failed.',
      );
    } finally {
      setIsCapturing(false);
    }
  }

  async function handleDeleteSignature(sigId: number) {
    try {
      await deleteSignature(result.documentId, sigId);
      setSignatures((prev) => prev.filter((s) => s.id !== sigId));
    } catch (err) {
      // silently log — the signature stays in the list if delete fails
      console.error('Failed to delete signature:', err);
    }
  }

  function triggerDownload(blob: Blob, filename: string) {
    const url = URL.createObjectURL(blob);
    const a   = document.createElement('a');
    a.href     = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  function handleExportJson() {
    const data = result.extractedFields.map((f) => ({
      field:      f.propertyName,
      value:      overrides[f.propertyName] || f.extractedValue || null,
      confidence: Math.round(f.confidence * 100),
    }));
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' });
    triggerDownload(blob, `${result.fileName}-fields.json`);
  }

  function handleExportCsv() {
    const header = 'Field,Value,Confidence %';
    const rows   = result.extractedFields.map((f) => {
      const val = (overrides[f.propertyName] || f.extractedValue || '').replace(/"/g, '""');
      return `"${f.propertyName}","${val}",${Math.round(f.confidence * 100)}`;
    });
    const blob = new Blob([[header, ...rows].join('\n')], { type: 'text/csv' });
    triggerDownload(blob, `${result.fileName}-fields.csv`);
  }

  const analyzedDate = new Date(result.analyzedAt).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  const effectivePageCount = result.pageCount > 0 ? result.pageCount : 1;

  return (
    <div className="flex flex-col gap-8">
      {/* ── Top row: extracted fields (left) + document preview (right) ──────── */}
      <div className="grid grid-cols-1 gap-8 lg:grid-cols-2">

        {/* ── Extracted fields ──────────────────────────────────────────────── */}
        <section aria-label="Extracted fields">
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-gray-500">
            Extracted Fields
          </h2>

          {result.extractedFields.length === 0 ? (
            <div className="rounded-xl border border-gray-200 bg-gray-50 px-5 py-6 text-center">
              <p className="text-sm text-gray-600">
                No fields were extracted. Try adjusting your OCR properties.
              </p>
            </div>
          ) : (
            <ExtractedFieldsTable
              fields={result.extractedFields}
              overrides={overrides}
              isSaving={isSaving}
              onOverrideChange={handleOverrideChange}
              signatures={signatures}
            />
          )}

          {/* Action buttons */}
          <div className="mt-4 flex flex-col gap-3">
            <div className="flex flex-wrap gap-2">
              <button
                type="button"
                onClick={handleSave}
                disabled={isSaving || result.extractedFields.length === 0}
                className="rounded-xl bg-blue-600 px-6 py-3 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
              >
                {isSaving ? (
                  <span className="flex items-center gap-2">
                    <svg aria-hidden="true" className="h-4 w-4 animate-spin" fill="none" viewBox="0 0 24 24">
                      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
                    </svg>
                    Saving…
                  </span>
                ) : 'Save Changes'}
              </button>

              {result.extractedFields.length > 0 && (
                <>
                  <button
                    type="button"
                    onClick={handleExportJson}
                    className="rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm font-semibold text-gray-700 transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-400 focus:ring-offset-2"
                  >
                    Export JSON
                  </button>
                  <button
                    type="button"
                    onClick={handleExportCsv}
                    className="rounded-xl border border-gray-200 bg-white px-4 py-3 text-sm font-semibold text-gray-700 transition-colors hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-gray-400 focus:ring-offset-2"
                  >
                    Export CSV
                  </button>
                </>
              )}
            </div>

            {saveStatus === 'success' && (
              <div role="status" aria-live="polite" className="rounded-xl border border-green-200 bg-green-50 px-5 py-3">
                <p className="text-sm font-semibold text-green-800">Changes saved successfully</p>
              </div>
            )}
            {saveStatus === 'error' && (
              <div role="alert" className="rounded-xl border border-red-200 bg-red-50 px-5 py-3">
                <p className="text-sm font-semibold text-red-800">Save failed</p>
                <p className="mt-0.5 text-xs text-red-700">{saveError}</p>
              </div>
            )}
          </div>
        </section>

        {/* ── Document preview ─────────────────────────────────────────────────── */}
        <section aria-label="Document preview and signature capture">
          <h2 className="mb-4 text-sm font-semibold uppercase tracking-wide text-gray-500">
            Document Preview
          </h2>

          <div className="flex flex-col gap-4 rounded-xl border border-gray-200 bg-white px-5 py-5">
            {/* Metadata */}
            <div className="flex flex-col gap-1">
              <p className="truncate text-sm font-semibold text-gray-800" title={result.fileName}>
                {result.fileName}
              </p>
              <dl className="flex flex-wrap gap-x-6 gap-y-1 text-xs text-gray-500">
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Analyzed:</dt>
                  <dd>{analyzedDate}</dd>
                </div>
                {result.documentType && result.documentType !== 'Unknown' && (
                  <div className="flex gap-1">
                    <dt className="font-medium text-gray-600">Type:</dt>
                    <dd>
                      <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-0.5 text-xs font-semibold text-blue-700 ring-1 ring-inset ring-blue-200">
                        {result.documentType}
                      </span>
                    </dd>
                  </div>
                )}
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Fields:</dt>
                  <dd>{result.extractedFields.length} {result.extractedFields.length === 1 ? 'field' : 'fields'} extracted</dd>
                </div>
                <div className="flex gap-1">
                  <dt className="font-medium text-gray-600">Document ID:</dt>
                  <dd>{result.documentId}</dd>
                </div>
              </dl>
            </div>

            {/* Signatures list */}
            {signatures.length > 0 && (
              <div className="flex flex-col gap-2">
                <p className="text-xs font-semibold uppercase tracking-wide text-gray-500">
                  Signatures ({signatures.length})
                </p>
                <div className="flex flex-col gap-2">
                  {signatures.map((sig) => (
                    <div
                      key={sig.id}
                      className="flex items-center gap-3 rounded-lg border border-emerald-200 bg-emerald-50 px-3 py-2"
                    >
                      <img
                        src={`data:image/jpeg;base64,${sig.imageData}`}
                        alt={sig.label ?? 'Signature'}
                        className="max-h-16 rounded border border-emerald-200 bg-white object-contain"
                      />
                      <div className="min-w-0 flex-1">
                        <span className="inline-flex items-center rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-800 ring-1 ring-inset ring-emerald-200">
                          {sig.label ?? 'Signature'}
                        </span>
                      </div>
                      <button
                        type="button"
                        aria-label={`Delete signature ${sig.label ?? sig.id}`}
                        onClick={() => handleDeleteSignature(sig.id)}
                        className="shrink-0 rounded-md p-1 text-gray-400 hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-400"
                      >
                        <svg className="h-4 w-4" fill="none" stroke="currentColor" strokeWidth={1.5} viewBox="0 0 24 24" aria-hidden="true">
                          <path strokeLinecap="round" strokeLinejoin="round" d="M6 18L18 6M6 6l12 12" />
                        </svg>
                      </button>
                    </div>
                  ))}
                </div>
              </div>
            )}

            {/* Mode toggle */}
            {imageUrl && (
              <div className="flex gap-0.5 self-start rounded-lg border border-gray-200 bg-gray-50 p-0.5">
                <button
                  type="button"
                  onClick={() => setPreviewMode('overlay')}
                  className={`rounded-md px-3 py-1.5 text-xs font-semibold transition-colors ${
                    previewMode === 'overlay'
                      ? 'bg-white text-gray-800 shadow-sm'
                      : 'text-gray-500 hover:text-gray-700'
                  }`}
                >
                  Word Overlay
                </button>
                <button
                  type="button"
                  onClick={() => setPreviewMode('capture')}
                  className={`rounded-md px-3 py-1.5 text-xs font-semibold transition-colors ${
                    previewMode === 'capture'
                      ? 'bg-white text-gray-800 shadow-sm'
                      : 'text-gray-500 hover:text-gray-700'
                  }`}
                >
                  Capture Signature
                </button>
              </div>
            )}

            {/* Word overlay */}
            {imageUrl && previewMode === 'overlay' && (
              <BoundingBoxOverlay
                imageUrl={imageUrl}
                blocks={result.textBlocks}
                currentPage={currentPage}
                pageCount={effectivePageCount}
                onPageChange={setCurrentPage}
              />
            )}

            {/* Signature capture canvas */}
            {imageUrl && previewMode === 'capture' && (
              <div className="flex flex-col gap-2">
                <p className="text-xs font-medium text-gray-500">
                  Draw a box over a signature area to add it to the list:
                </p>
                <SignatureCanvas
                  imageUrl={imageUrl}
                  onCapture={handleCapture}
                  isSaving={isCapturing}
                  captureError={captureError}
                  pageCount={result.pageCount}
                  currentPage={currentPage}
                  onPageChange={setCurrentPage}
                />
              </div>
            )}

            {/* Raw OCR text — collapsed by default */}
            <details className="group">
              <summary className="cursor-pointer select-none text-xs font-medium text-gray-600 hover:text-gray-900 focus:outline-none">
                <span className="group-open:hidden">Show Raw OCR Text</span>
                <span className="hidden group-open:inline">Hide Raw OCR Text</span>
              </summary>
              <pre className="mt-3 max-h-64 overflow-auto whitespace-pre-wrap rounded-lg bg-gray-50 p-3 text-xs text-gray-700 ring-1 ring-gray-200">
                {result.rawText || '(no text extracted)'}
              </pre>
            </details>
          </div>

          <div className="mt-4">
            <Link href="/upload" className="text-sm font-medium text-blue-600 hover:text-blue-800 focus:outline-none focus:underline">
              ← Analyze another document
            </Link>
          </div>
        </section>
      </div>

      {/* ── OCR tokens + detected tables (full width below) ───────────────────── */}
      <OcrTokensTable blocks={result.textBlocks} />
      <TableBlocksView tables={result.tableBlocks ?? []} />
    </div>
  );
}
