'use client';

import { useState, Fragment } from 'react';
import Link from 'next/link';
import type { DocumentSummary } from '@/types/ocr';

interface Props {
  documents: DocumentSummary[];
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

export default function DocumentsClient({ documents }: Props) {
  const [expandedId, setExpandedId] = useState<number | null>(null);

  if (documents.length === 0) {
    return (
      <div className="rounded-xl border border-gray-200 bg-gray-50 px-5 py-12 text-center">
        <p className="text-sm font-semibold text-gray-700">No documents analyzed yet.</p>
        <p className="mt-1 text-xs text-gray-500">Upload a document to get started.</p>
        <Link
          href="/upload"
          className="mt-4 inline-block rounded-lg bg-blue-600 px-4 py-2 text-sm font-semibold text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
        >
          Upload Document
        </Link>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
      <table className="min-w-full divide-y divide-gray-100 text-sm">
        <thead>
          <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
            <th className="w-8 px-4 py-3" />
            <th className="px-4 py-3">Document Name</th>
            <th className="px-4 py-3">Type</th>
            <th className="px-4 py-3">Analyzed Date</th>
            <th className="px-4 py-3">Fields Extracted</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {documents.map((doc) => {
            const isExpanded = expandedId === doc.id;
            const analyzedDate = new Date(doc.analyzedAt).toLocaleString(undefined, {
              dateStyle: 'medium',
              timeStyle: 'short',
            });

            return (
              <Fragment key={doc.id}>
                <tr
                  className="cursor-pointer transition-colors hover:bg-gray-50"
                  onClick={() => setExpandedId(isExpanded ? null : doc.id)}
                  aria-expanded={isExpanded}
                >
                  <td className="px-4 py-3 text-gray-400">
                    <svg
                      className={`h-3.5 w-3.5 transition-transform ${isExpanded ? 'rotate-90' : ''}`}
                      viewBox="0 0 20 20"
                      fill="currentColor"
                      aria-hidden="true"
                    >
                      <path fillRule="evenodd" d="M7.293 14.707a1 1 0 010-1.414L10.586 10 7.293 6.707a1 1 0 011.414-1.414l4 4a1 1 0 010 1.414l-4 4a1 1 0 01-1.414 0z" clipRule="evenodd" />
                    </svg>
                  </td>
                  <td className="max-w-xs truncate px-4 py-3 font-medium text-gray-800" title={doc.fileName}>
                    {doc.fileName}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    {doc.documentType && doc.documentType !== 'Unknown' ? (
                      <span className="inline-flex items-center rounded-full bg-blue-50 px-2 py-0.5 text-xs font-semibold text-blue-700 ring-1 ring-inset ring-blue-200">
                        {doc.documentType}
                      </span>
                    ) : (
                      <span className="text-xs text-gray-400">—</span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-600">{analyzedDate}</td>
                  <td className="px-4 py-3 text-gray-600">{doc.fieldCount}</td>
                </tr>

                {isExpanded && (
                  <tr>
                    <td colSpan={5} className="bg-gray-50 px-8 py-4">
                      {doc.fields.length === 0 ? (
                        <p className="text-xs italic text-gray-400">No extracted values for this document.</p>
                      ) : (
                        <>
                          <div className="overflow-x-auto rounded-lg border border-gray-200 bg-white">
                            <table className="min-w-full divide-y divide-gray-100 text-sm">
                              <thead>
                                <tr className="bg-gray-50 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">
                                  <th className="px-4 py-2">Field Name</th>
                                  <th className="px-4 py-2">Extracted Value</th>
                                  <th className="px-4 py-2 text-right">Confidence</th>
                                </tr>
                              </thead>
                              <tbody className="divide-y divide-gray-100">
                                {doc.fields.map((field) => (
                                  <tr key={field.propertyName}>
                                    <td className="whitespace-nowrap px-4 py-2 font-medium text-gray-700">
                                      {field.propertyName}
                                    </td>
                                    <td className="px-4 py-2 text-gray-600">
                                      {field.extractedValue ?? <span className="italic text-gray-400">—</span>}
                                    </td>
                                    <td className="px-4 py-2 text-right">
                                      <ConfidenceBadge value={field.confidence} />
                                    </td>
                                  </tr>
                                ))}
                              </tbody>
                            </table>
                          </div>
                          <div className="mt-3 flex justify-end">
                            <Link
                              href={`/results/${doc.id}`}
                              onClick={(e) => e.stopPropagation()}
                              className="rounded-md bg-blue-50 px-3 py-1.5 text-xs font-semibold text-blue-700 transition-colors hover:bg-blue-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
                            >
                              View Full Results →
                            </Link>
                          </div>
                        </>
                      )}
                    </td>
                  </tr>
                )}
              </Fragment>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}
