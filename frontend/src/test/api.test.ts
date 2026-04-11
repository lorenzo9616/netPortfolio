import { describe, it, expect, vi, beforeEach } from 'vitest';
import { login, listDocuments, deleteDocument, getProperties, ApiError } from '@/lib/api';

function mockFetch(status: number, body: unknown, ok = status < 400) {
  return vi.fn().mockResolvedValue({
    ok,
    status,
    statusText: String(status),
    json: () => Promise.resolve(body),
  });
}

describe('ApiError', () => {
  it('sets name, message, and status', () => {
    const err = new ApiError('bad thing', 400);
    expect(err.name).toBe('ApiError');
    expect(err.message).toBe('bad thing');
    expect(err.status).toBe(400);
  });
});

describe('login()', () => {
  beforeEach(() => vi.restoreAllMocks());

  it('returns token on 200', async () => {
    vi.stubGlobal('fetch', mockFetch(200, { token: 'tok', expiresAt: '2099-01-01' }));
    const result = await login('admin', 'pass');
    expect(result.token).toBe('tok');
  });

  it('throws ApiError with status 401 on bad credentials', async () => {
    vi.stubGlobal('fetch', mockFetch(401, 'Unauthorized'));
    await expect(login('admin', 'wrong')).rejects.toMatchObject({ status: 401 });
  });
});

describe('listDocuments()', () => {
  it('calls correct URL and returns page data', async () => {
    const page = { items: [], totalCount: 0, page: 1, pageSize: 20 };
    vi.stubGlobal('fetch', mockFetch(200, page));
    const result = await listDocuments(1);
    expect(result.totalCount).toBe(0);
    expect((vi.mocked(fetch)).mock.calls[0][0]).toContain('/api/documents');
  });
});

describe('deleteDocument()', () => {
  it('resolves void on 204', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, status: 204, json: () => Promise.resolve(undefined) }));
    await expect(deleteDocument(1)).resolves.toBeUndefined();
  });

  it('throws ApiError on 404', async () => {
    vi.stubGlobal('fetch', mockFetch(404, 'Not found'));
    await expect(deleteDocument(999)).rejects.toMatchObject({ status: 404 });
  });
});

describe('getProperties()', () => {
  it('returns array of properties', async () => {
    const props = [{ id: 1, name: 'Test', dataType: 'string', isActive: true }];
    vi.stubGlobal('fetch', mockFetch(200, props));
    const result = await getProperties();
    expect(result).toHaveLength(1);
    expect(result[0].name).toBe('Test');
  });
});
