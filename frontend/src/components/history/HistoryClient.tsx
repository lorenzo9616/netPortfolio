'use client';

import { useState, useEffect, useCallback } from 'react';
import Link from 'next/link';
import { listDocuments, deleteDocument, ApiError } from '@/lib/api';
import type { DocumentHistoryItem, DocumentHistoryPage } from '@/types/ocr';

type LoadState =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'loaded'; data: DocumentHistoryPage };

export default function HistoryClient() {
  const [page, setPage]           = useState(1);
  const [loadState, setLoadState] = useState<LoadState>({ status: 'loading' });
  const [deletingId, setDeletingId] = useState<number | null>(null);
  const [deleteError, setDeleteError] = useState<{ id: number; message: string } | null>(null);

  const fetchPage = useCallback(async (p: number) => {
    setLoadState({ status: 'loading' });
    try {
      const data = await listDocuments(p);
      setLoadState({ status: 'loaded', data });
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Failed to load history.';
      setLoadState({ status: 'error', message });
    }
  }, []);

  useEffect(() => {
    fetchPage(page);
  }, [page, fetchPage]);

  async function handleDelete(item: DocumentHistoryItem) {
    const confirmed = window.confirm('Delete this result? This cannot be undone.');
    if (!confirmed) return;

    setDeletingId(item.documentId);
    setDeleteError(null);

    try {
      await deleteDocument(item.documentId);
      const newPage = (() => {
        if (loadState.status !== 'loaded') return page;
        const remaining = loadState.data.items.length - 1;
        return remaining === 0 && page > 1 ? page - 1 : page;
      })();
      setPage(newPage);
      if (newPage === page) fetchPage(page);
    } catch (err) {
      const message = err instanceof ApiError ? err.message : 'Delete failed.';
      setDeleteError({ id: item.documentId, message });
    } finally {
      setDeletingId(null);
    }
  }

  function formatDate(iso: string) {
    return new Date(iso).toLocaleString('en-US', {
      month: 'short', day: 'numeric', year: 'numeric',
      hour: 'numeric', minute: '2-digit',
    });
  }

  if (loadState.status === 'loading') {
    return (
      <div className="space-y-3">
        {[...Array(5)].map((_, i) => (
          <div key={i} className="h-16 animate-pulse rounded-xl bg-gray-100" />
        ))}
      </div>
    );
  }

  if (loadState.status === 'error') {
    return (
      <div className="rounded-xl border border-red-200 bg-red-50 px-5 py-4">
        <p className="text-sm font-semibold text-red-800">Could not load history</p>
        <p className="mt-1 text-xs text-red-700">{loadState.message}</p>
        <button type="button" onClick={() => fetchPage(page)}
          className="mt-3 text-xs font-medium text-red-700 underline hover:no-underline">
          Retry
        </button>
      </div>
    );
  }

  const { items, totalCount, pageSize } = loadState.data;
  const totalPages = Math.ceil(totalCount / pageSize);
  const start = (page - 1) * pageSize + 1;
  const end   = Math.min(page * pageSize, totalCount);

  if (items.length === 0 && page === 1) {
    return (
      <div className="flex flex-col items-center gap-4 py-16 text-center">
        <p className="text-gray-500">No documents analyzed yet.</p>
        <Link href="/upload"
          className="rounded-xl bg-blue-600 px-5 py-2.5 text-sm font-semibold text-white hover:bg-blue-700">
          Upload your first document
        </Link>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm text-gray-500">
        Showing {start}–{end} of {totalCount} result{totalCount !== 1 ? 's' : ''}
      </p>

      <div className="overflow-hidden rounded-xl border border-gray-200 bg-white">
        <table className="min-w-full divide-y divide-gray-100">
          <thead className="bg-gray-50">
            <tr>
              <th className="px-5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Date</th>
              <th className="px-5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Pages</th>
              <th className="px-5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Fields</th>
              <th className="px-5 py-3 text-left text-xs font-semibold uppercase tracking-wide text-gray-500">Preview</th>
              <th className="px-5 py-3" />
            </tr>
          </thead>
          <tbody className="divide-y divide-gray-100">
            {items.map((item) => (
              <tr key={item.documentId} className="hover:bg-gray-50">
                <td className="whitespace-nowrap px-5 py-4 text-sm text-gray-700">{formatDate(item.createdAt)}</td>
                <td className="px-5 py-4 text-sm text-gray-500">{item.pageCount}</td>
                <td className="px-5 py-4 text-sm text-gray-500">{item.fieldCount}</td>
                <td className="max-w-xs px-5 py-4 text-sm text-gray-500">
                  <p className="truncate">{item.preview || '—'}</p>
                  {deleteError?.id === item.documentId && (
                    <p className="mt-1 text-xs text-red-600">{deleteError.message}</p>
                  )}
                </td>
                <td className="px-5 py-4">
                  <div className="flex items-center justify-end gap-2">
                    <Link href={`/results/${item.documentId}`}
                      className="rounded-lg bg-blue-50 px-3 py-1.5 text-xs font-medium text-blue-700 hover:bg-blue-100">
                      View
                    </Link>
                    <button type="button" disabled={deletingId === item.documentId}
                      onClick={() => handleDelete(item)}
                      className="rounded-lg bg-red-50 px-3 py-1.5 text-xs font-medium text-red-700 hover:bg-red-100 disabled:opacity-50">
                      {deletingId === item.documentId ? 'Deleting…' : 'Delete'}
                    </button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {totalPages > 1 && (
        <div className="flex items-center justify-between">
          <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-40">
            Previous
          </button>
          <span className="text-sm text-gray-500">Page {page} of {totalPages}</span>
          <button type="button" disabled={page >= totalPages} onClick={() => setPage((p) => p + 1)}
            className="rounded-lg border border-gray-200 px-4 py-2 text-sm font-medium text-gray-600 hover:bg-gray-50 disabled:opacity-40">
            Next
          </button>
        </div>
      )}
    </div>
  );
}
