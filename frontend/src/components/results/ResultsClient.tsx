'use client';

import { useState, useEffect, useRef } from 'react';
import Link from 'next/link';
import type { AnalysisResultDetail, SavedFieldUpdate } from '@/types/ocr';
import { saveFieldOverrides, ApiError } from '@/lib/api';

// ─── Props ────────────────────────────────────────────────────────────────────

interface Props {
  result: AnalysisResultDetail;
}

// ─── Confidence badge ─────────────────────────────────────────────────────────

function ConfidenceBadge({ value }: { value: number }) {
  const pct = Math.round(value * 100);
  const colorClass =
    pct > 80
      ? 'bg-green-100 text-green-800 ring-green-200'
      : pct > 50
      ? 'bg-yellow-100 text-yellow-800 ring-yellow-200'
      : 'bg-red-100 text-red-800 ring-red-200';

  return (
    <span
      className={`inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium ring-1 ring-inset ${colorClass}`}
    >
      {pct}%
    </span>
  );
}

// ─── Component ────────────────────────────────────────────────────────────────

export default function ResultsClient({ result }: Props) {
  // Keyed by propertyName → current override input value
  const [overrides, setOverrides] = useState<Record<string, string>>(() => {
    const initial: Record<string, string> = {};
    for (const field of result.extractedFields) {
      initial[field.propertyName] = field.extractedValue ?? '';
    }
    return initial;
  });

  const [isSaving, setIsSaving] = useState(false);
  const [saveStatus, setSaveStatus] = useState<'idle' | 'success' | 'error'>('idle');
  const [saveError, setSaveError] = useState<string>('');
  const dismissTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Auto-dismiss success banner after 3 s — clean up on unmount or re-trigger
  useEffect(() => {
    if (saveStatus !== 'success') return;

    dismissTimerRef.current = setTimeout(() => {
      setSaveStatus('idle');
    }, 3000);

    return () => {
      if (dismissTimerRef.current !== null) {
        clearTimeout(dismissTimerRef.current);
        dismissTimerRef.current = null;
      }
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
      propertyName: f.propertyName,
      manualOverride: overrides[f.propertyName] ?? null,
    }));

    try {
      await saveFieldOverrides(result.documentId, payload);
      setSaveStatus('success');
    } catch (err) {
      const message =
        err instanceof ApiError
          ? err.message
          : err instanceof Error
          ? err.message
          : 'An unexpected error occurred while saving.';
      setSaveError(message);
      setSaveStatus('error');
    } finally {
      setIsSaving(false);
    }
  }

  const analyzedDate = new Date(result.analyzedAt).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });

  return (
    <div className="grid grid-cols-1 gap-8 lg:grid-cols-2">
      {/* ── Left column: extracted fields ─────────────────────────────────── */}
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
                {result.extractedFields.map((field) => (
                  <tr key={field.propertyName} className="align-middle">
                    <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-800">
                      {field.propertyName}
                    </td>
                    <td className="px-4 py-3 text-gray-600">
                      {field.extractedValue ?? (
                        <span className="italic text-gray-400">—</span>
                      )}
                    </td>
                    <td className="px-4 py-3">
                      <input
                        type="text"
                        aria-label={`Manual override for ${field.propertyName}`}
                        value={overrides[field.propertyName] ?? ''}
                        onChange={(e) =>
                          handleOverrideChange(field.propertyName, e.target.value)
                        }
                        disabled={isSaving}
                        className="w-full rounded-md border border-gray-300 bg-white px-2 py-1 text-sm text-gray-800 placeholder-gray-400 focus:border-blue-500 focus:outline-none focus:ring-1 focus:ring-blue-500 disabled:cursor-not-allowed disabled:bg-gray-50 disabled:text-gray-400"
                        placeholder="Enter override…"
                      />
                    </td>
                    <td className="px-4 py-3 text-right">
                      <ConfidenceBadge value={field.confidence} />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {/* Save button & banners */}
        <div className="mt-4 flex flex-col gap-3">
          <button
            type="button"
            onClick={handleSave}
            disabled={isSaving || result.extractedFields.length === 0}
            className="w-full rounded-xl bg-blue-600 px-6 py-3 text-sm font-semibold text-white transition-colors hover:bg-blue-700 disabled:cursor-not-allowed disabled:opacity-40 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 sm:w-auto"
          >
            {isSaving ? (
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
                Saving…
              </span>
            ) : (
              'Save Changes'
            )}
          </button>

          {saveStatus === 'success' && (
            <div
              role="status"
              aria-live="polite"
              className="rounded-xl border border-green-200 bg-green-50 px-5 py-3"
            >
              <p className="text-sm font-semibold text-green-800">
                Changes saved successfully
              </p>
            </div>
          )}

          {saveStatus === 'error' && (
            <div
              role="alert"
              className="rounded-xl border border-red-200 bg-red-50 px-5 py-3"
            >
              <p className="text-sm font-semibold text-red-800">Save failed</p>
              <p className="mt-0.5 text-xs text-red-700">{saveError}</p>
            </div>
          )}
        </div>
      </section>

      {/* ── Right column: document preview ────────────────────────────────── */}
      <section aria-label="Document preview">
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
              <div className="flex gap-1">
                <dt className="font-medium text-gray-600">Fields:</dt>
                <dd>
                  {result.extractedFields.length}{' '}
                  {result.extractedFields.length === 1 ? 'field' : 'fields'} extracted
                </dd>
              </div>
              <div className="flex gap-1">
                <dt className="font-medium text-gray-600">Document ID:</dt>
                <dd>{result.documentId}</dd>
              </div>
            </dl>
          </div>

          {/* Image preview note */}
          <div className="rounded-lg border border-dashed border-gray-300 bg-gray-50 px-4 py-5 text-center">
            <p className="text-xs text-gray-500">
              Image preview not available — document was processed server-side.
            </p>
          </div>

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

        {/* Back link */}
        <div className="mt-4">
          <Link
            href="/upload"
            className="text-sm font-medium text-blue-600 hover:text-blue-800 focus:outline-none focus:underline"
          >
            ← Analyze another document
          </Link>
        </div>
      </section>
    </div>
  );
}
