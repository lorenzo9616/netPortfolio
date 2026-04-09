'use client';

import { useState } from 'react';
import type { CreateOcrPropertyDto, UpdateOcrPropertyDto } from '@/types/ocr';

const DATA_TYPE_OPTIONS = ['string', 'date', 'decimal', 'boolean'] as const;

interface FormValues {
  name: string;
  dataType: string;
  searchHeuristic: string;
  isActive: boolean;
}

interface PropertyFormProps {
  initialValues?: Partial<FormValues>;
  onSubmit: (dto: CreateOcrPropertyDto | UpdateOcrPropertyDto) => void;
  onCancel: () => void;
  isLoading: boolean;
}

function validate(values: FormValues): Partial<Record<keyof FormValues, string>> {
  const errors: Partial<Record<keyof FormValues, string>> = {};
  if (!values.name.trim()) {
    errors.name = 'Name is required.';
  } else if (values.name.trim().length < 2) {
    errors.name = 'Name must be at least 2 characters.';
  }
  return errors;
}

export default function PropertyForm({
  initialValues,
  onSubmit,
  onCancel,
  isLoading,
}: PropertyFormProps) {
  const [values, setValues] = useState<FormValues>({
    name: initialValues?.name ?? '',
    dataType: initialValues?.dataType ?? 'string',
    searchHeuristic: initialValues?.searchHeuristic ?? '',
    isActive: initialValues?.isActive ?? true,
  });
  const [errors, setErrors] = useState<Partial<Record<keyof FormValues, string>>>({});
  const [touched, setTouched] = useState<Partial<Record<keyof FormValues, boolean>>>({});

  function handleChange(
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>,
  ) {
    const { name, value, type } = e.target;
    const newVal =
      type === 'checkbox' ? (e.target as HTMLInputElement).checked : value;
    setValues((prev) => ({ ...prev, [name]: newVal }));
    if (touched[name as keyof FormValues]) {
      setErrors(validate({ ...values, [name]: newVal }));
    }
  }

  function handleBlur(e: React.FocusEvent<HTMLInputElement | HTMLSelectElement>) {
    const { name } = e.target;
    setTouched((prev) => ({ ...prev, [name]: true }));
    setErrors(validate(values));
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const allTouched: Partial<Record<keyof FormValues, boolean>> = {
      name: true,
      dataType: true,
      searchHeuristic: true,
      isActive: true,
    };
    setTouched(allTouched);
    const errs = validate(values);
    setErrors(errs);
    if (Object.keys(errs).length > 0) return;

    const dto: CreateOcrPropertyDto = {
      name: values.name.trim(),
      dataType: values.dataType,
      searchHeuristic: values.searchHeuristic.trim() || null,
      isActive: values.isActive,
    };
    onSubmit(dto);
  }

  return (
    <form onSubmit={handleSubmit} noValidate className="space-y-5">
      {/* Name */}
      <div>
        <label htmlFor="name" className="block text-sm font-medium text-gray-700">
          Name <span className="text-red-500" aria-hidden="true">*</span>
        </label>
        <input
          id="name"
          name="name"
          type="text"
          value={values.name}
          onChange={handleChange}
          onBlur={handleBlur}
          required
          aria-required="true"
          aria-describedby={errors.name ? 'name-error' : undefined}
          className={`mt-1 block w-full rounded-lg border px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500 ${
            errors.name && touched.name
              ? 'border-red-500 focus:ring-red-500'
              : 'border-gray-300'
          }`}
        />
        {errors.name && touched.name && (
          <p id="name-error" role="alert" className="mt-1 text-xs text-red-600">
            {errors.name}
          </p>
        )}
      </div>

      {/* Data Type */}
      <div>
        <label htmlFor="dataType" className="block text-sm font-medium text-gray-700">
          Data Type
        </label>
        <select
          id="dataType"
          name="dataType"
          value={values.dataType}
          onChange={handleChange}
          onBlur={handleBlur}
          className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        >
          {DATA_TYPE_OPTIONS.map((opt) => (
            <option key={opt} value={opt}>
              {opt.charAt(0).toUpperCase() + opt.slice(1)}
            </option>
          ))}
        </select>
      </div>

      {/* Search Heuristic */}
      <div>
        <label
          htmlFor="searchHeuristic"
          className="block text-sm font-medium text-gray-700"
        >
          Search Heuristic{' '}
          <span className="text-gray-400 font-normal">(optional)</span>
        </label>
        <input
          id="searchHeuristic"
          name="searchHeuristic"
          type="text"
          value={values.searchHeuristic}
          onChange={handleChange}
          className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2 text-sm shadow-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
        />
      </div>

      {/* Is Active */}
      <div className="flex items-center gap-3">
        <input
          id="isActive"
          name="isActive"
          type="checkbox"
          checked={values.isActive}
          onChange={handleChange}
          className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
        />
        <label htmlFor="isActive" className="text-sm font-medium text-gray-700">
          Active
        </label>
      </div>

      {/* Actions */}
      <div className="flex justify-end gap-3 pt-2">
        <button
          type="button"
          onClick={onCancel}
          disabled={isLoading}
          className="rounded-lg border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 disabled:opacity-50"
        >
          Cancel
        </button>
        <button
          type="submit"
          disabled={isLoading}
          className="inline-flex items-center gap-2 rounded-lg bg-blue-600 px-4 py-2 text-sm font-medium text-white hover:bg-blue-700 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2 disabled:opacity-50"
        >
          {isLoading && (
            <svg
              className="h-4 w-4 animate-spin"
              xmlns="http://www.w3.org/2000/svg"
              fill="none"
              viewBox="0 0 24 24"
              aria-hidden="true"
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
          )}
          {isLoading ? 'Saving…' : 'Save'}
        </button>
      </div>
    </form>
  );
}
