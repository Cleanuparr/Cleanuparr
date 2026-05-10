import { TEST_CONFIG } from '../test-config';

export type RawJson = unknown;

export interface ApiClientOptions {
  baseUrl?: string;
  token?: string;
}

/**
 * Thin fetch wrapper used by every feature-area API module.
 * One instance per test (or one shared admin client for setup).
 */
export class ApiClient {
  baseUrl: string;
  token?: string;

  constructor(opts: ApiClientOptions = {}) {
    this.baseUrl = opts.baseUrl ?? TEST_CONFIG.appUrl;
    this.token = opts.token;
  }

  setToken(token: string | undefined): void {
    this.token = token;
  }

  private buildHeaders(extra?: HeadersInit): HeadersInit {
    const headers: Record<string, string> = {};
    if (this.token) {
      headers['Authorization'] = `Bearer ${this.token}`;
    }
    if (extra) {
      const entries = extra instanceof Headers ? Array.from(extra.entries()) : Object.entries(extra as Record<string, string>);
      for (const [k, v] of entries) {
        headers[k] = v;
      }
    }
    return headers;
  }

  request(path: string, init: RequestInit = {}): Promise<Response> {
    const url = path.startsWith('http') ? path : `${this.baseUrl}${path}`;
    return fetch(url, {
      ...init,
      headers: this.buildHeaders(init.headers),
    });
  }

  async get(path: string): Promise<Response> {
    return this.request(path);
  }

  async getJson<T = RawJson>(path: string): Promise<T> {
    const res = await this.request(path);
    if (!res.ok) {
      throw new Error(`GET ${path} failed: ${res.status} ${await res.text()}`);
    }
    return res.json() as Promise<T>;
  }

  async post(path: string, body?: unknown): Promise<Response> {
    return this.request(path, {
      method: 'POST',
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : {},
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  }

  async postJson<T = RawJson>(path: string, body?: unknown): Promise<T> {
    const res = await this.post(path, body);
    if (!res.ok) {
      throw new Error(`POST ${path} failed: ${res.status} ${await res.text()}`);
    }
    return res.json() as Promise<T>;
  }

  async put(path: string, body?: unknown): Promise<Response> {
    return this.request(path, {
      method: 'PUT',
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : {},
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  }

  async putJson<T = RawJson>(path: string, body?: unknown): Promise<T> {
    const res = await this.put(path, body);
    if (!res.ok) {
      throw new Error(`PUT ${path} failed: ${res.status} ${await res.text()}`);
    }
    return res.json() as Promise<T>;
  }

  async delete(path: string): Promise<Response> {
    return this.request(path, { method: 'DELETE' });
  }
}

export async function waitForApp(client: ApiClient, timeoutMs = 90_000): Promise<void> {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(`${client.baseUrl}/health`);
      if (res.ok) {
        return;
      }
    } catch {
      // not ready yet
    }
    await new Promise((r) => setTimeout(r, 2000));
  }
  throw new Error(`App did not become ready within ${timeoutMs}ms`);
}
