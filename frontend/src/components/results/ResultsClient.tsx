'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import type { AnalysisResultDetail, SavedFieldUpdate } from '@/types/ocr';
import { saveFieldOverrides, captureSignature, ApiError } from '@/lib/api';
import SignatureCanvas from './SignatureCanvas';
import OcrTokensTable from './OcrTokensTable';
import BoundingBoxOverlay from './BoundingBoxOverlay';
import TableBlocksView from './TableBlocksView';

interface Props {
  result: AnalysisResultDetail;
}

function ConfidenceBadge({ value }: { value: number }) {
  const pct = Math.round(value * 100);
  const colorClass =
    pct > 80
      ? 'bg-green-100 text-green-800 ring-green-200'
      : pct > 50
      ? 'bg-yellow-100 text-yellow-800 ring-yellow-200'
      : 'bg-red-100 text-red-800 ring-red-200';
  return (
    <span className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${colorClass}`}>
      {pct}%
    </span>
  );
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

  const [signatureImage, setSignatureImage] = useState<string | null>(
    result.signatureImage ?? null,
  );
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
      const resp = await captureSignature(result.documentId, base64Png);
      setSignatureImage(resp.imageData);
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
            <>
              {(() => {
                const filledFields = result.extractedFields.filter(
                  (f) => f.extractedValue !== null && f.extractedValue !== ''
                );
                const emptyFields = result.extractedFields.filter(
                  (f) => f.extractedValue === null || f.extractedValue === ''
                );

                const renderRows = (fields: typeof result.extractedFields) =>
                  fields.map((field) => {
                    const isSignatureField = field.propertyName === 'Signature';
                    return (
                      <tr key={field.propertyName} className="align-middle">
                        <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-800">
                          {field.propertyName}
                        </td>
                        <td className="px-4 py-3 text-gray-600">
                          {isSignatureField && signatureImage
                            ? <span className="inline-flex items-center rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-700 ring-1 ring-inset ring-emerald-200">Auto-detected</span>
                            : (field.extractedValue ?? <span className="italic text-gray-400">—</span>)
                          }
                        </td>
                        <td className="px-4 py-3">
                          {isSignatureField
                            ? <span className="text-xs italic text-gray-400">See preview above</span>
                            : (
                              <input
                                type="text"
                                aria-label={`Manual override for ${field.propertyName}`}
                                value={overrides[field.propertyName] ?? ''}
                                onChange={(e) => handleOverrideChange(field.propertyName, e.target.value)}
                                disabled={isSaving}
                                className="w-full rounded-md border border-gray-300 bg-white px-2 py-1 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:cursor-not-allowed disabled:bg-gray-50"
                                placeholder="Enter override…"
                              />
                            )
                          }
                        </td>
                        <td className="px-4 py-3 text-right">
                          <ConfidenceBadge value={field.confidence} />
                        </td>
                      </tr>
                    );
                  });

                return (
                  <div className="flex flex-col gap-3">
                    {filledFields.length > 0 && (
                      <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
                        <table className="min-w-full divide-y divide-gray-100 text-sm">
                          <thead>
                            <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
                              <th className="px-4 py-3">Field Name</th>
                              <th className="px-4 py-3">Extracted Value</th>
                              <th className="px-4 py-3">Manual Override</th>
                              <th className="px-4 py-3 text-right">Confidence</th>
                            </tr>
                          </thead>
                          <tbody className="divide-y divide-gray-100">
                            {renderRows(filledFields)}
                          </tbody>
                        </table>
                      </div>
                    )}

                    {emptyFields.length > 0 && (
                      <details className="group rounded-xl border border-gray-200 bg-white">
                        <summary className="cursor-pointer select-none px-4 py-3 text-xs font-semibold uppercase tracking-wide text-gray-400 hover:text-gray-600 focus:outline-none list-none flex items-center gap-2">
                          <svg className="h-3.5 w-3.5 transition-transform group-open:rotate-90" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">
                            <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
                          </svg>
                          {emptyFields.length} empty {emptyFields.length === 1 ? 'field' : 'fields'}
                        </summary>
                        <div className="border-t border-gray-100">
                          <table className="min-w-full divide-y divide-gray-100 text-sm">
                            <thead>
                              <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
                                <th className="px-4 py-3">Field Name</th>
                                <th className="px-4 py-3">Extracted Value</th>
                                <th className="px-4 py-3">Manual Override</th>
                                <th className="px-4 py-3 text-right">Confidence</th>
                              </tr>
                            </thead>
                            <tbody className="divide-y divide-gray-100">
                              {renderRows(emptyFields)}
                            </tbody>
                          </table>
                        </div>
                      </details>
                    )}
                  </div>
                );
              })()}
            </>
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

            {/* Auto-detected signature preview */}
            {signatureImage && (
              <div className="rounded-lg border border-emerald-200 bg-emerald-50 px-4 py-3">
                <div className="mb-2 flex items-center gap-2">
                  <span className="inline-flex items-center rounded-full bg-emerald-100 px-2 py-0.5 text-xs font-semibold text-emerald-800 ring-1 ring-inset ring-emerald-200">
                    Auto-detected
                  </span>
                  <span className="text-xs font-medium text-emerald-800">Signature</span>
                </div>
                <img
                  src={`data:image/jpeg;base64,${signatureImage}`}
                  alt="Auto-detected signature"
                  className="max-h-24 rounded border border-emerald-200 bg-white"
                />
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
                  {signatureImage
                    ? 'Re-capture signature (draw a box over the signature area):'
                    : 'Capture signature manually:'}
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
