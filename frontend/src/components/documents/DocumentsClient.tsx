'use client';

import Link from 'next/link';
import type { DocumentSummary } from '@/types/ocr';

interface Props {
  documents: DocumentSummary[];
}

export default function DocumentsClient({ documents }: Props) {
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
            <th className="px-4 py-3">ID</th>
            <th className="px-4 py-3">File Name</th>
            <th className="px-4 py-3">Analyzed At</th>
            <th className="px-4 py-3">Fields</th>
            <th className="px-4 py-3">Pages</th>
            <th className="px-4 py-3 text-right">Action</th>
          </tr>
        </thead>
        <tbody className="divide-y divide-gray-100">
          {documents.map((doc) => (
            <tr key={doc.id} className="transition-colors hover:bg-gray-50">
              <td className="px-4 py-3 text-gray-400">#{doc.id}</td>
              <td
                className="max-w-xs truncate px-4 py-3 font-medium text-gray-800"
                title={doc.fileName}
              >
                {doc.fileName}
              </td>
              <td className="whitespace-nowrap px-4 py-3 text-gray-600">
                {new Date(doc.analyzedAt).toLocaleString(undefined, {
                  dateStyle: 'medium',
                  timeStyle: 'short',
                })}
              </td>
              <td className="px-4 py-3 text-gray-600">{doc.fieldCount}</td>
              <td className="px-4 py-3 text-gray-600">{doc.pageCount || '—'}</td>
              <td className="px-4 py-3 text-right">
                <Link
                  href={`/results/${doc.id}`}
                  className="rounded-md bg-blue-50 px-3 py-1 text-xs font-semibold text-blue-700 transition-colors hover:bg-blue-100"
                >
                  View Results
                </Link>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
