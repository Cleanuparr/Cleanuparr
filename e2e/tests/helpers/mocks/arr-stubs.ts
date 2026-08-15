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

/**
 * Serves a queue page from a raw JSON body.
 *
 * WireMock writes a `jsonBody` with a JSON serializer, and the serializer drops
 * the decimal point of a whole number. A raw body keeps each number as written.
 * Sonarr v3 sends `sizeleft` as `4467066880.0`, and a test needs that exact
 * shape.
 */
export function arrRawQueueStub(body: string): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/queue' },
    response: {
      status: 200,
      headers: { 'Content-Type': 'application/json' },
      body,
    },
  };
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

/** Serves the command list that SeekerCommandMonitor polls. An absent command is a forgotten command. */
export function arrCommandListStub(commands: Array<{ id: number; status: string }>): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/command' },
    response: { status: 200, jsonBody: commands },
  };
}

export function arrCommandNotFoundStub(commandId: number): Mapping {
  return {
    request: { method: 'GET', urlPath: `/api/v3/command/${commandId}` },
    response: { status: 404, jsonBody: { message: 'Not Found' } },
  };
}

export interface SearchableMovie {
  id: number;
  title: string;
  monitored?: boolean;
  hasFile?: boolean;
  status?: string;
  qualityProfileId?: number;
  tags?: number[];
  digitalRelease?: string;
}

/** Serves the Radarr library. Defaults make each movie a proactive search candidate. */
export function arrMoviesStub(movies: SearchableMovie[]): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/movie' },
    response: {
      status: 200,
      jsonBody: movies.map((movie) => ({
        monitored: true,
        hasFile: false,
        status: 'released',
        qualityProfileId: 1,
        tags: [],
        digitalRelease: '2020-01-01T00:00:00Z',
        ...movie,
      })),
    },
  };
}

export function arrQualityProfilesStub(): Mapping {
  return {
    request: { method: 'GET', urlPath: '/api/v3/qualityprofile' },
    response: { status: 200, jsonBody: [{ id: 1, name: 'Any', cutoff: 1, items: [] }] },
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
