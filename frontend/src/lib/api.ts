import type {
  OcrProperty,
  CreateOcrPropertyDto,
  UpdateOcrPropertyDto,
  CropRegion,
  AnalyzeResponse,
  SavedFieldUpdate,
  ExtractedField,
  AnalysisResultDetail,
  DocumentHistoryPage,
} from '@/types/ocr';
import { getToken } from './auth';

export const API_BASE =
  process.env.NEXT_PUBLIC_API_URL ?? 'http://localhost:5000';

export class ApiError extends Error {
  constructor(
    public readonly message: string,
    public readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

async function handleResponse<T>(res: Response): Promise<T> {
  if (!res.ok) {
    let message = `HTTP ${res.status}`;
    try {
      const body = await res.json();
      if (typeof body === 'object' && body !== null && 'message' in body) {
        message = String((body as { message: unknown }).message);
      } else if (typeof body === 'string') {
        message = body;
      }
    } catch {
      // fallback to status text
      message = res.statusText || message;
    }
    throw new ApiError(message, res.status);
  }
  // 204 No Content
  if (res.status === 204) {
    return undefined as unknown as T;
  }
  return res.json() as Promise<T>;
}

function authHeaders(): HeadersInit {
  const token = getToken();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

export async function getProperties(): Promise<OcrProperty[]> {
  const res = await fetch(`${API_BASE}/api/ocrproperties`, {
    cache: 'no-store',
    headers: { ...authHeaders() },
  });
  return handleResponse<OcrProperty[]>(res);
}

export async function createProperty(
  dto: CreateOcrPropertyDto,
): Promise<OcrProperty> {
  const res = await fetch(`${API_BASE}/api/ocrproperties`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(dto),
  });
  return handleResponse<OcrProperty>(res);
}

export async function updateProperty(
  id: number,
  dto: UpdateOcrPropertyDto,
): Promise<OcrProperty> {
  const res = await fetch(`${API_BASE}/api/ocrproperties/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(dto),
  });
  return handleResponse<OcrProperty>(res);
}

export async function deleteProperty(id: number): Promise<void> {
  const res = await fetch(`${API_BASE}/api/ocrproperties/${id}`, {
    method: 'DELETE',
    headers: { ...authHeaders() },
  });
  return handleResponse<void>(res);
}

export async function getAnalysisResult(id: number): Promise<AnalysisResultDetail> {
  const res = await fetch(`${API_BASE}/api/documents/${id}`, {
    cache: 'no-store',
    headers: { ...authHeaders() },
  });
  return handleResponse<AnalysisResultDetail>(res);
}

export async function saveFieldOverrides(
  documentId: number,
  fields: SavedFieldUpdate[],
): Promise<ExtractedField[]> {
  const res = await fetch(`${API_BASE}/api/documents/${documentId}/fields`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json', ...authHeaders() },
    body: JSON.stringify(fields),
  });
  if (!res.ok) throw new ApiError(`Save failed: ${res.statusText}`, res.status);
  return res.json() as Promise<ExtractedField[]>;
}

export async function analyzeDocument(
  file: File,
  crop?: CropRegion,
): Promise<AnalyzeResponse> {
  const formData = new FormData();
  formData.append('file', file);
  if (crop) {
    formData.append('cropX', String(crop.x));
    formData.append('cropY', String(crop.y));
    formData.append('cropWidth', String(crop.width));
    formData.append('cropHeight', String(crop.height));
  }
  const res = await fetch(`${API_BASE}/api/documents/analyze`, {
    method: 'POST',
    body: formData,
    headers: { ...authHeaders() },
    // No Content-Type header — let browser set multipart boundary
  });
  if (!res.ok) throw new ApiError(`Analysis failed: ${res.statusText}`, res.status);
  return res.json() as Promise<AnalyzeResponse>;
}

export async function login(username: string, password: string): Promise<{ token: string; expiresAt: string }> {
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ username, password }),
  });
  return handleResponse<{ token: string; expiresAt: string }>(res);
}

export async function listDocuments(page = 1): Promise<DocumentHistoryPage> {
  const res = await fetch(`${API_BASE}/api/documents?page=${page}&pageSize=20`, {
    cache: 'no-store',
    headers: { ...authHeaders() },
  });
  return handleResponse<DocumentHistoryPage>(res);
}

export async function deleteDocument(id: number): Promise<void> {
  const res = await fetch(`${API_BASE}/api/documents/${id}`, {
    method: 'DELETE',
    headers: { ...authHeaders() },
  });
  return handleResponse<void>(res);
}
