'use client';

import { useState, useCallback } from 'react';
import type { OcrProperty, CreateOcrPropertyDto, UpdateOcrPropertyDto } from '@/types/ocr';
import { createProperty, updateProperty, deleteProperty, getProperties } from '@/lib/api';
import Modal from '@/components/ui/Modal';
import PropertyForm from '@/components/properties/PropertyForm';
import { ApiError } from '@/lib/api';

interface PropertiesListProps {
  initialProperties: OcrProperty[];
}

type ModalMode = 'create' | 'edit';

// Pencil icon
function PencilIcon({ className }: { className?: string }) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      className={className}
      viewBox="0 0 20 20"
      fill="currentColor"
      aria-hidden="true"
    >
      <path d="M13.586 3.586a2 2 0 112.828 2.828l-.793.793-2.828-2.828.793-.793zM11.379 5.793L3 14.172V17h2.828l8.38-8.379-2.83-2.828z" />
    </svg>
  );
}

// Trash icon
function TrashIcon({ className }: { className?: string }) {
  return (
    <svg
      xmlns="http://www.w3.org/2000/svg"
      className={className}
      viewBox="0 0 20 20"
      fill="currentColor"
      aria-hidden="true"
    >
      <path
        fillRule="evenodd"
        d="M9 2a1 1 0 00-.894.553L7.382 4H4a1 1 0 000 2v10a2 2 0 002 2h8a2 2 0 002-2V6a1 1 0 100-2h-3.382l-.724-1.447A1 1 0 0011 2H9zM7 8a1 1 0 012 0v6a1 1 0 11-2 0V8zm5-1a1 1 0 00-1 1v6a1 1 0 102 0V8a1 1 0 00-1-1z"
        clipRule="evenodd"
      />
    </svg>
  );
}

// Spinner icon
function Spinner() {
  return (
    <svg
      className="h-5 w-5 animate-spin text-blue-600"
      xmlns="http://www.w3.org/2000/svg"
      fill="none"
      viewBox="0 0 24 24"
      aria-hidden="true"
    >
      <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
      <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8v8H4z" />
    </svg>
  );
}

export default function PropertiesList({ initialProperties }: PropertiesListProps) {
  const [properties, setProperties] = useState<OcrProperty[]>(initialProperties);
  const [modalOpen, setModalOpen] = useState(false);
  const [modalMode, setModalMode] = useState<ModalMode>('create');
  const [editTarget, setEditTarget] = useState<OcrProperty | null>(null);
  const [isMutating, setIsMutating] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const refreshProperties = useCallback(async () => {
    const fresh = await getProperties();
    setProperties(fresh);
  }, []);

  function openCreate() {
    setEditTarget(null);
    setModalMode('create');
    setErrorMsg(null);
    setModalOpen(true);
  }

  function openEdit(property: OcrProperty) {
    setEditTarget(property);
    setModalMode('edit');
    setErrorMsg(null);
    setModalOpen(true);
  }

  function closeModal() {
    if (isMutating) return;
    setModalOpen(false);
    setEditTarget(null);
    setErrorMsg(null);
  }

  async function handleSubmit(dto: CreateOcrPropertyDto | UpdateOcrPropertyDto) {
    setIsMutating(true);
    setErrorMsg(null);
    try {
      if (modalMode === 'create') {
        const created = await createProperty(dto as CreateOcrPropertyDto);
        setProperties((prev) => [...prev, created]);
      } else if (editTarget) {
        const updated = await updateProperty(editTarget.id, dto as UpdateOcrPropertyDto);
        setProperties((prev) =>
          prev.map((p) => (p.id === updated.id ? updated : p)),
        );
      }
      setModalOpen(false);
      setEditTarget(null);
    } catch (err) {
      if (err instanceof ApiError) {
        setErrorMsg(`Error ${err.status}: ${err.message}`);
      } else {
        setErrorMsg('An unexpected error occurred.');
      }
    } finally {
      setIsMutating(false);
    }
  }

  async function handleDelete(property: OcrProperty) {
    if (!window.confirm(`Delete property "${property.name}"? This cannot be undone.`)) {
      return;
    }
    setIsMutating(true);
    setErrorMsg(null);
    try {
      await deleteProperty(property.id);
      setProperties((prev) => prev.filter((p) => p.id !== property.id));
    } catch (err) {
      if (err instanceof ApiError) {
        setErrorMsg(`Error ${err.status}: ${err.message}`);
      } else {
        setErrorMsg('Failed to delete property.');
      }
      // Re-sync in case of partial state issues
      try {
        await refreshProperties();
      } catch {
        // ignore secondary errors
      }
    } finally {
      setIsMutating(false);
    }
  }

  const modalTitle = modalMode === 'create' ? 'New Property' : 'Edit Property';

  const initialFormValues = editTarget
    ? {
        name: editTarget.name,
        dataType: editTarget.dataType,
        searchHeuristic: editTarget.searchHeuristic ?? '',
        isActive: editTarget.isActive,
      }
    : undefined;

  return (
    <div>
      {/* Header row */}
      <div className="mb-6 flex items-center justify-between">
        <h1 className="text-2xl font-bold text-gray-900">OCR Properties</h1>
        <button
          type="button"
          onClick={openCreate}
          disabled={isMutating}
          className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50"
        >
          {isMutating && !modalOpen ? <Spinner /> : null}
          + New Property
        </button>
      </div>

      {/* Global error banner */}
      {errorMsg && !modalOpen && (
        <div
          role="alert"
          className="mb-4 flex items-center justify-between rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"
        >
          <span>{errorMsg}</span>
          <button
            type="button"
            onClick={() => setErrorMsg(null)}
            aria-label="Dismiss error"
            className="ml-4 rounded p-0.5 hover:bg-red-100"
          >
            ×
          </button>
        </div>
      )}

      {/* Table */}
      <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white shadow-sm">
        {properties.length === 0 ? (
          <div className="py-16 text-center text-gray-500 text-sm">
            No properties found.{' '}
            <button
              type="button"
              onClick={openCreate}
              className="text-blue-600 underline hover:text-blue-700"
            >
              Create one
            </button>
            .
          </div>
        ) : (
          <table className="min-w-full divide-y divide-gray-200 text-sm">
            <thead className="bg-gray-50">
              <tr>
                {['Name', 'Data Type', 'Search Heuristic', 'Active', 'Created', 'Actions'].map(
                  (col) => (
                    <th
                      key={col}
                      scope="col"
                      className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wider text-gray-500"
                    >
                      {col}
                    </th>
                  ),
                )}
              </tr>
            </thead>
            <tbody className="divide-y divide-gray-100">
              {properties.map((property) => (
                <tr key={property.id} className="hover:bg-gray-50 transition-colors">
                  <td className="whitespace-nowrap px-4 py-3 font-medium text-gray-900">
                    {property.name}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-600">
                    <span className="inline-flex items-center rounded-full bg-gray-100 px-2.5 py-0.5 text-xs font-medium text-gray-700">
                      {property.dataType}
                    </span>
                  </td>
                  <td className="px-4 py-3 text-gray-600">
                    {property.searchHeuristic ?? (
                      <span className="italic text-gray-400">—</span>
                    )}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    <span
                      className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${
                        property.isActive
                          ? 'bg-green-100 text-green-700'
                          : 'bg-gray-100 text-gray-500'
                      }`}
                    >
                      {property.isActive ? 'Yes' : 'No'}
                    </span>
                  </td>
                  <td className="whitespace-nowrap px-4 py-3 text-gray-500">
                    {new Date(property.createdAt).toLocaleDateString()}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3">
                    <div className="flex items-center gap-2">
                      <button
                        type="button"
                        onClick={() => openEdit(property)}
                        disabled={isMutating}
                        aria-label={`Edit ${property.name}`}
                        className="rounded p-1.5 text-gray-400 hover:bg-blue-50 hover:text-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
                      >
                        <PencilIcon className="h-4 w-4" />
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDelete(property)}
                        disabled={isMutating}
                        aria-label={`Delete ${property.name}`}
                        className="rounded p-1.5 text-gray-400 hover:bg-red-50 hover:text-red-600 focus:outline-none focus:ring-2 focus:ring-red-500 disabled:opacity-50"
                      >
                        <TrashIcon className="h-4 w-4" />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {/* Create / Edit Modal */}
      <Modal isOpen={modalOpen} onClose={closeModal} title={modalTitle}>
        {/* Error inside modal */}
        {errorMsg && (
          <div
            role="alert"
            className="mb-4 rounded-lg bg-red-50 border border-red-200 px-4 py-3 text-sm text-red-700"
          >
            {errorMsg}
          </div>
        )}
        <PropertyForm
          key={editTarget?.id ?? 'create'}
          initialValues={initialFormValues}
          onSubmit={handleSubmit}
          onCancel={closeModal}
          isLoading={isMutating}
        />
      </Modal>
    </div>
  );
}
