import type { Mapping, WireMockClient } from './wiremock-client';

/**
 * Convenience stub bundles for Sonarr/Radarr/Lidarr/Readarr/Whisparr.
 * Tests register only the stubs they need; defaults can be pulled in via {@link applyArrDefaults}.
 */

export interface ArrHealthStubOptions {
  apiKey?: string;
  status?: number;
  version?: string;
}

export function arrHealthStub(opts: ArrHealthStubOptions = {}): Mapping {
  return {
    request: {
      method: 'GET',
      urlPathPattern: '/api/v[0-9]+/system/status',
      ...(opts.apiKey
        ? { headers: { 'X-Api-Key': { equalTo: opts.apiKey } } }
        : {}),
    },
    response: {
      status: opts.status ?? 200,
      jsonBody: { version: opts.version ?? '4.0.0', appName: 'Sonarr' },
    },
  };
}

export function arrUnauthorizedStub(urlPathPattern = '/api/v3/.*'): Mapping {
  return {
    request: { method: 'ANY', urlPathPattern },
    response: { status: 401, jsonBody: { message: 'Unauthorized' } },
    priority: 10,
  };
}

export function arrEmptyQueueStub(): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/queue' },
    response: {
      status: 200,
      jsonBody: { page: 1, pageSize: 50, totalRecords: 0, records: [] },
    },
  };
}

export interface QueueRecord {
  id: number;
  title: string;
  status: string;
  trackedDownloadStatus?: string;
  trackedDownloadState?: string;
  errorMessage?: string;
  downloadId: string;
  protocol?: string;
  estimatedCompletionTime?: string;
}

export function arrQueueStub(records: QueueRecord[]): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/queue' },
    response: {
      status: 200,
      jsonBody: {
        page: 1,
        pageSize: records.length,
        totalRecords: records.length,
        records,
      },
    },
  };
}

export function arrCommandTriggerStub(commandId = 1): Mapping {
  return {
    request: { method: 'POST', urlPath: '/api/v3/command' },
    response: {
      status: 201,
      jsonBody: { id: commandId, name: 'AutomaticSearch', status: 'queued' },
    },
  };
}

export function arrCommandCompletedStub(commandId: number, status = 'completed'): Mapping {
  return {
    request: { method: 'GET', urlPath: `/api/v3/command/${commandId}` },
    response: { status: 200, jsonBody: { id: commandId, status } },
  };
}

export function arrTagsStub(tags: Array<{ id: number; label: string }> = []): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/tag' },
    response: { status: 200, jsonBody: tags },
  };
}

export function arrCustomFormatsStub(): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/customformat' },
    response: { status: 200, jsonBody: [] },
  };
}

export async function applyArrDefaults(arr: WireMockClient): Promise<void> {
  await arr.stubMany([
    arrHealthStub(),
    arrEmptyQueueStub(),
    arrTagsStub(),
    arrCustomFormatsStub(),
  ]);
}
