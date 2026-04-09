'use client';

import { useRef, useState } from 'react';

interface FileDropzoneProps {
  onFileSelected: (file: File) => void;
  onCameraToggle: () => void;
}

const ACCEPTED_TYPES = new Set(['application/pdf', 'image/png', 'image/jpeg']);
const ACCEPTED_EXTENSIONS = new Set(['.pdf', '.png', '.jpg', '.jpeg']);
const MAX_BYTES = 20 * 1024 * 1024; // 20 MB

function validateFile(file: File): string | null {
  const ext = '.' + file.name.split('.').pop()?.toLowerCase();
  const typeOk = ACCEPTED_TYPES.has(file.type) || ACCEPTED_EXTENSIONS.has(ext);
  if (!typeOk) return 'Only PDF, PNG, and JPG files are allowed.';
  if (file.size > MAX_BYTES) return 'File size must be 20 MB or less.';
  return null;
}

export default function FileDropzone({ onFileSelected, onCameraToggle }: FileDropzoneProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  const [isDragOver, setIsDragOver] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function handleFile(file: File) {
    const err = validateFile(file);
    if (err) {
      setError(err);
      return;
    }
    setError(null);
    onFileSelected(file);
  }

  function handleDragOver(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragOver(true);
  }

  function handleDragLeave(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragOver(false);
  }

  function handleDrop(e: React.DragEvent<HTMLDivElement>) {
    e.preventDefault();
    setIsDragOver(false);
    const file = e.dataTransfer.files[0];
    if (file) handleFile(file);
  }

  function handleInputChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (file) handleFile(file);
    // Reset input so the same file can be re-selected after error
    e.target.value = '';
  }

  function handleClick() {
    inputRef.current?.click();
  }

  function handleKeyDown(e: React.KeyboardEvent<HTMLDivElement>) {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      inputRef.current?.click();
    }
  }

  return (
    <div className="flex flex-col gap-4">
      {/* Drop zone */}
      <div
        role="button"
        tabIndex={0}
        aria-label="Click to browse files or drag and drop a PDF, PNG, or JPG here"
        onClick={handleClick}
        onKeyDown={handleKeyDown}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onDrop={handleDrop}
        className={`
          flex cursor-pointer flex-col items-center justify-center
          rounded-xl border-2 border-dashed p-10 text-center
          transition-colors focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2
          ${isDragOver
            ? 'border-blue-500 bg-blue-50 text-blue-700'
            : 'border-gray-300 bg-white text-gray-500 hover:border-blue-400 hover:bg-blue-50 hover:text-blue-600'
          }
        `}
      >
        {/* Upload icon */}
        <svg
          aria-hidden="true"
          className="mb-3 h-12 w-12 opacity-60"
          fill="none"
          stroke="currentColor"
          strokeWidth={1.5}
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M3 16.5v2.25A2.25 2.25 0 005.25 21h13.5A2.25 2.25 0 0021 18.75V16.5m-13.5-9L12 3m0 0l4.5 4.5M12 3v13.5"
          />
        </svg>

        <p className="text-sm font-medium">
          {isDragOver ? 'Drop file here' : 'Drag & drop a file here'}
        </p>
        <p className="mt-1 text-xs">or click to browse</p>
        <p className="mt-2 text-xs text-gray-400">PDF, PNG, JPG — max 20 MB</p>

        <input
          ref={inputRef}
          type="file"
          accept=".pdf,.png,.jpg,.jpeg"
          aria-label="Upload document file"
          className="sr-only"
          onChange={handleInputChange}
        />
      </div>

      {/* Inline error */}
      {error && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-4 py-2 text-sm text-red-700">
          {error}
        </p>
      )}

      {/* Divider */}
      <div className="flex items-center gap-3 text-xs text-gray-400">
        <hr className="flex-1 border-gray-200" />
        <span>or</span>
        <hr className="flex-1 border-gray-200" />
      </div>

      {/* Camera button */}
      <button
        type="button"
        aria-label="Use device camera to capture a photo"
        onClick={onCameraToggle}
        className="flex items-center justify-center gap-2 rounded-lg border border-gray-300 bg-white px-4 py-2.5 text-sm font-medium text-gray-700 transition-colors hover:border-blue-400 hover:bg-blue-50 hover:text-blue-600 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-2"
      >
        <svg
          aria-hidden="true"
          className="h-5 w-5"
          fill="none"
          stroke="currentColor"
          strokeWidth={1.5}
          viewBox="0 0 24 24"
        >
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M6.827 6.175A2.31 2.31 0 015.186 7.23c-.38.054-.757.112-1.134.175C2.999 7.58 2.25 8.507 2.25 9.574V18a2.25 2.25 0 002.25 2.25h15A2.25 2.25 0 0021.75 18V9.574c0-1.067-.75-1.994-1.802-2.169a47.865 47.865 0 00-1.134-.175 2.31 2.31 0 01-1.64-1.055l-.822-1.316a2.192 2.192 0 00-1.736-1.039 48.774 48.774 0 00-5.232 0 2.192 2.192 0 00-1.736 1.039l-.821 1.316z"
          />
          <path
            strokeLinecap="round"
            strokeLinejoin="round"
            d="M16.5 12.75a4.5 4.5 0 11-9 0 4.5 4.5 0 019 0zM18.75 10.5h.008v.008h-.008V10.5z"
          />
        </svg>
        Use Camera
      </button>
    </div>
  );
}
