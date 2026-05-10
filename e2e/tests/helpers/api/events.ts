import { ApiClient } from './client';

export interface EventsQuery {
  page?: number;
  pageSize?: number;
  severity?: string;
  eventType?: string;
  fromDate?: string;
  toDate?: string;
  search?: string;
  jobRunId?: string;
}

function toQs(query?: Record<string, unknown>): string {
  if (!query) {
    return '';
  }
  const entries = Object.entries(query)
    .filter(([, v]) => v !== undefined && v !== null)
    .map(([k, v]) => [k, String(v)] as [string, string]);
  if (!entries.length) {
    return '';
  }
  return '?' + new URLSearchParams(entries).toString();
}

export class EventsApi {
  constructor(private readonly client: ApiClient) {}

  list(query?: EventsQuery): Promise<Response> {
    return this.client.get(`/api/events${toQs(query as Record<string, unknown>)}`);
  }

  get(id: string): Promise<Response> {
    return this.client.get(`/api/events/${id}`);
  }

  byTracking(trackingId: string): Promise<Response> {
    return this.client.get(`/api/events/tracking/${trackingId}`);
  }

  types(): Promise<Response> {
    return this.client.get('/api/events/types');
  }

  severities(): Promise<Response> {
    return this.client.get('/api/events/severities');
  }

  cleanup(retentionDays = 30): Promise<Response> {
    return this.client.post(`/api/events/cleanup?retentionDays=${retentionDays}`);
  }
}

export class ManualEventsApi {
  constructor(private readonly client: ApiClient) {}

  list(query?: Record<string, unknown>): Promise<Response> {
    return this.client.get(`/api/manual-events${toQs(query)}`);
  }

  get(id: string): Promise<Response> {
    return this.client.get(`/api/manual-events/${id}`);
  }

  resolve(id: string): Promise<Response> {
    return this.client.post(`/api/manual-events/${id}/resolve`);
  }

  stats(): Promise<Response> {
    return this.client.get('/api/manual-events/stats');
  }

  severities(): Promise<Response> {
    return this.client.get('/api/manual-events/severities');
  }

  cleanup(retentionDays = 30): Promise<Response> {
    return this.client.post(`/api/manual-events/cleanup?retentionDays=${retentionDays}`);
  }
}

export class StrikesApi {
  constructor(private readonly client: ApiClient) {}

  list(query?: Record<string, unknown>): Promise<Response> {
    return this.client.get(`/api/strikes${toQs(query)}`);
  }

  recent(count = 5): Promise<Response> {
    return this.client.get(`/api/strikes/recent?count=${count}`);
  }

  types(): Promise<Response> {
    return this.client.get('/api/strikes/types');
  }

  delete(downloadItemId: string): Promise<Response> {
    return this.client.delete(`/api/strikes/${downloadItemId}`);
  }
}

export class StatsApi {
  constructor(private readonly client: ApiClient) {}

  get(query?: { hours?: number; includeEvents?: number; includeStrikes?: number }): Promise<Response> {
    return this.client.get(`/api/stats${toQs(query)}`);
  }
}
