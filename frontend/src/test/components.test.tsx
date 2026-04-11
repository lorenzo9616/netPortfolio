import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

// Mock next/navigation
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), refresh: vi.fn() }),
}));

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ href, children }: { href: string; children: React.ReactNode }) => (
    <a href={href}>{children}</a>
  ),
}));

// Mock api module
vi.mock('@/lib/api', () => ({
  login: vi.fn(),
  listDocuments: vi.fn(),
  deleteDocument: vi.fn(),
  ApiError: class ApiError extends Error {
    status: number;
    constructor(message: string, status: number) {
      super(message);
      this.name = 'ApiError';
      this.status = status;
    }
  },
}));

// Mock auth module
vi.mock('@/lib/auth', () => ({
  setToken: vi.fn(),
  clearToken: vi.fn(),
  getToken: vi.fn(() => 'mock-token'),
  TOKEN_COOKIE: 'ocr_token',
}));

import LoginPage from '@/app/login/page';
import HistoryClient from '@/components/history/HistoryClient';
import PropertyForm from '@/components/properties/PropertyForm';
import FileDropzone from '@/components/upload/FileDropzone';
import { login, listDocuments } from '@/lib/api';

// ── LoginPage ─────────────────────────────────────────────────────────────────

describe('LoginPage', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders username and password fields', () => {
    render(<LoginPage />);
    expect(screen.getByLabelText(/username/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
  });

  it('shows error on 401', async () => {
    const { ApiError } = await import('@/lib/api');
    vi.mocked(login).mockRejectedValue(new ApiError('Unauthorized', 401));
    render(<LoginPage />);
    await userEvent.type(screen.getByLabelText(/username/i), 'admin');
    await userEvent.type(screen.getByLabelText(/password/i), 'wrong');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));
    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(/incorrect username or password/i),
    );
  });

  it('shows generic error on non-401 failure', async () => {
    vi.mocked(login).mockRejectedValue(new Error('Network error'));
    render(<LoginPage />);
    await userEvent.type(screen.getByLabelText(/username/i), 'admin');
    await userEvent.type(screen.getByLabelText(/password/i), 'pass');
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }));
    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(/something went wrong/i),
    );
  });
});

// ── HistoryClient ─────────────────────────────────────────────────────────────

describe('HistoryClient', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders loading skeletons initially', () => {
    vi.mocked(listDocuments).mockReturnValue(new Promise(() => {}));
    render(<HistoryClient />);
    // 5 skeleton divs with animate-pulse class
    const skeletons = document.querySelectorAll('.animate-pulse');
    expect(skeletons.length).toBeGreaterThan(0);
  });

  it('shows empty state when no documents', async () => {
    vi.mocked(listDocuments).mockResolvedValue({
      items: [], totalCount: 0, page: 1, pageSize: 20,
    });
    render(<HistoryClient />);
    await waitFor(() =>
      expect(screen.getByText(/no documents analyzed yet/i)).toBeInTheDocument(),
    );
  });

  it('renders document rows when data is loaded', async () => {
    vi.mocked(listDocuments).mockResolvedValue({
      items: [{
        documentId: 1, createdAt: '2024-01-01T00:00:00Z',
        pageCount: 2, fieldCount: 3, preview: 'Hello world',
      }],
      totalCount: 1, page: 1, pageSize: 20,
    });
    render(<HistoryClient />);
    await waitFor(() => expect(screen.getByText('Hello world')).toBeInTheDocument());
    expect(screen.getByText('2')).toBeInTheDocument(); // pageCount
    expect(screen.getByText('3')).toBeInTheDocument(); // fieldCount
  });
});

// ── PropertyForm ──────────────────────────────────────────────────────────────

describe('PropertyForm', () => {
  const onSubmit = vi.fn();
  const onCancel = vi.fn();

  beforeEach(() => vi.clearAllMocks());

  it('renders all fields', () => {
    render(<PropertyForm onSubmit={onSubmit} onCancel={onCancel} isLoading={false} />);
    expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
    expect(screen.getByRole('combobox')).toBeInTheDocument(); // dataType select
    expect(screen.getByLabelText(/active/i)).toBeInTheDocument();
  });

  it('shows validation error when name is empty on submit', async () => {
    render(<PropertyForm onSubmit={onSubmit} onCancel={onCancel} isLoading={false} />);
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('calls onSubmit with correct dto when valid', async () => {
    render(<PropertyForm onSubmit={onSubmit} onCancel={onCancel} isLoading={false} />);
    await userEvent.type(screen.getByLabelText(/name/i), 'Invoice Number');
    await userEvent.click(screen.getByRole('button', { name: /save/i }));
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ name: 'Invoice Number', dataType: 'string', isActive: true }),
    );
  });

  it('calls onCancel when cancel is clicked', async () => {
    render(<PropertyForm onSubmit={onSubmit} onCancel={onCancel} isLoading={false} />);
    await userEvent.click(screen.getByRole('button', { name: /cancel/i }));
    expect(onCancel).toHaveBeenCalled();
  });
});

// ── FileDropzone ──────────────────────────────────────────────────────────────

describe('FileDropzone', () => {
  const onFileSelected = vi.fn();
  const onCameraToggle = vi.fn();

  beforeEach(() => vi.clearAllMocks());

  it('renders upload area', () => {
    render(<FileDropzone onFileSelected={onFileSelected} onCameraToggle={onCameraToggle} />);
    // Should render a file input
    expect(document.querySelector('input[type="file"]')).toBeInTheDocument();
  });

  it('calls onFileSelected with valid PNG file', async () => {
    render(<FileDropzone onFileSelected={onFileSelected} onCameraToggle={onCameraToggle} />);
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['content'], 'test.png', { type: 'image/png' });
    await userEvent.upload(input, file);
    expect(onFileSelected).toHaveBeenCalledWith(file);
  });

  it('shows error for unsupported file type', async () => {
    render(<FileDropzone onFileSelected={onFileSelected} onCameraToggle={onCameraToggle} />);
    const input = document.querySelector('input[type="file"]') as HTMLInputElement;
    const file = new File(['content'], 'test.txt', { type: 'text/plain' });
    // applyAccept: false bypasses the HTML accept filter so the onChange fires
    await userEvent.setup({ applyAccept: false }).upload(input, file);
    expect(onFileSelected).not.toHaveBeenCalled();
    await waitFor(() =>
      expect(screen.getByText(/only pdf, png, and jpg/i)).toBeInTheDocument(),
    );
  });
});
