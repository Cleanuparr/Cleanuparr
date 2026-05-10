import { ApiClient } from './client';

export class StatusApi {
  constructor(private readonly client: ApiClient) {}

  system(): Promise<Response> {
    return this.client.get('/api/status');
  }

  downloadClients(): Promise<Response> {
    return this.client.get('/api/status/download-client');
  }

  arrs(): Promise<Response> {
    return this.client.get('/api/status/arrs');
  }
}

export class HealthApi {
  constructor(private readonly client: ApiClient) {}

  liveness(): Promise<Response> {
    return this.client.get('/health');
  }

  readiness(): Promise<Response> {
    return this.client.get('/health/ready');
  }

  detailed(): Promise<Response> {
    return this.client.get('/health/detailed');
  }

  downloadClients(): Promise<Response> {
    return this.client.get('/api/health');
  }

  downloadClient(id: string): Promise<Response> {
    return this.client.get(`/api/health/${id}`);
  }

  triggerCheck(): Promise<Response> {
    return this.client.post('/api/health/check');
  }

  triggerCheckOne(id: string): Promise<Response> {
    return this.client.post(`/api/health/check/${id}`);
  }
}
