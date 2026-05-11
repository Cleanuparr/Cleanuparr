import { TEST_CONFIG } from '../test-config';

export type RequestMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH' | 'ANY';

export interface MappingRequest {
  method: RequestMethod;
  url?: string;
  urlPath?: string;
  urlPathPattern?: string;
  urlPattern?: string;
  bodyPatterns?: Array<Record<string, unknown>>;
  headers?: Record<
    string,
    { equalTo?: string; matches?: string; contains?: string; absent?: boolean }
  >;
  queryParameters?: Record<string, { equalTo?: string; matches?: string }>;
}

export interface MappingResponse {
  status?: number;
  body?: string;
  jsonBody?: unknown;
  bodyFileName?: string;
  headers?: Record<string, string>;
  fixedDelayMilliseconds?: number;
}

export interface Mapping {
  request: MappingRequest;
  response: MappingResponse;
  priority?: number;
  metadata?: Record<string, unknown>;
}

export interface RequestLogEntry {
  request: {
    method: string;
    url: string;
    headers: Record<string, string | string[]>;
    body?: string;
    bodyAsBase64?: string;
  };
  response: {
    status: number;
    body?: string;
  };
  loggedDate: number;
}

/**
 * Thin wrapper over the WireMock admin API. One instance per stub container.
 * https://wiremock.org/docs/standalone/admin-api-reference/
 */
export class WireMockClient {
  constructor(public readonly adminUrl: string) {}

  async stub(mapping: Mapping): Promise<{ id: string }> {
    const res = await fetch(`${this.adminUrl}/mappings`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(mapping),
    });
    if (!res.ok) {
      throw new Error(`Failed to register WireMock stub at ${this.adminUrl}: ${res.status} ${await res.text()}`);
    }
    const data = await res.json();
    return { id: data.id };
  }

  async stubMany(mappings: Mapping[]): Promise<void> {
    await Promise.all(mappings.map((m) => this.stub(m)));
  }

  async resetMappings(): Promise<void> {
    const res = await fetch(`${this.adminUrl}/mappings`, { method: 'DELETE' });
    if (!res.ok) {
      throw new Error(`Failed to reset WireMock mappings at ${this.adminUrl}: ${res.status}`);
    }
  }

  async resetAll(): Promise<void> {
    const res = await fetch(`${this.adminUrl}/reset`, { method: 'POST' });
    if (!res.ok) {
      throw new Error(`Failed to reset WireMock at ${this.adminUrl}: ${res.status}`);
    }
  }

  async requests(): Promise<RequestLogEntry[]> {
    const res = await fetch(`${this.adminUrl}/requests`);
    if (!res.ok) {
      throw new Error(`Failed to fetch WireMock requests at ${this.adminUrl}: ${res.status}`);
    }
    const data = await res.json();
    return data.requests ?? [];
  }

  async findRequests(criteria: { method?: string; urlPath?: string; urlPattern?: string }): Promise<RequestLogEntry[]> {
    const res = await fetch(`${this.adminUrl}/requests/find`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(criteria),
    });
    if (!res.ok) {
      throw new Error(`Failed to find WireMock requests at ${this.adminUrl}: ${res.status}`);
    }
    const data = await res.json();
    return data.requests ?? [];
  }

  async waitForRequest(
    criteria: { method?: string; urlPath?: string; urlPattern?: string },
    timeoutMs = 30_000,
  ): Promise<RequestLogEntry> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      const matches = await this.findRequests(criteria);
      if (matches.length > 0) {
        return matches[0];
      }
      await new Promise((r) => setTimeout(r, 250));
    }
    throw new Error(`Timed out waiting for WireMock request matching ${JSON.stringify(criteria)}`);
  }

  async waitReady(timeoutMs = 60_000): Promise<void> {
    const start = Date.now();
    while (Date.now() - start < timeoutMs) {
      try {
        const res = await fetch(`${this.adminUrl}/mappings`);
        if (res.ok) {
          return;
        }
      } catch {
        // not ready yet
      }
      await new Promise((r) => setTimeout(r, 1_000));
    }
    throw new Error(`WireMock at ${this.adminUrl} did not become ready within ${timeoutMs}ms`);
  }
}

/**
 * Aggregate of all WireMock containers, one per external integration class.
 * Each test holds a single instance via fixtures and resets all mappings on teardown.
 */
export class MockServers {
  readonly arr: WireMockClient;
  readonly downloadClient: WireMockClient;
  readonly notify: WireMockClient;
  readonly blocklist: WireMockClient;

  constructor() {
    this.arr = new WireMockClient(TEST_CONFIG.mocks.arrAdminUrl);
    this.downloadClient = new WireMockClient(TEST_CONFIG.mocks.downloadClientAdminUrl);
    this.notify = new WireMockClient(TEST_CONFIG.mocks.notifyAdminUrl);
    this.blocklist = new WireMockClient(TEST_CONFIG.mocks.blocklistAdminUrl);
  }

  async resetAll(): Promise<void> {
    await Promise.all([
      this.arr.resetAll(),
      this.downloadClient.resetAll(),
      this.notify.resetAll(),
      this.blocklist.resetAll(),
    ]);
  }

  async waitReady(timeoutMs = 60_000): Promise<void> {
    await Promise.all([
      this.arr.waitReady(timeoutMs),
      this.downloadClient.waitReady(timeoutMs),
      this.notify.waitReady(timeoutMs),
      this.blocklist.waitReady(timeoutMs),
    ]);
  }

  get urls() {
    return {
      arr: TEST_CONFIG.mocks.arrUrl,
      downloadClient: TEST_CONFIG.mocks.downloadClientUrl,
      notify: TEST_CONFIG.mocks.notifyUrl,
      blocklist: TEST_CONFIG.mocks.blocklistUrl,
    };
  }
}
