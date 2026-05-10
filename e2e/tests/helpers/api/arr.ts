import { ApiClient } from './client';

export type ArrType = 'sonarr' | 'radarr' | 'lidarr' | 'readarr' | 'whisparr';

export interface ArrInstancePayload {
  name: string;
  url: string;
  apiKey: string;
  version?: number;
  enabled?: boolean;
}

export class ArrApi {
  constructor(private readonly client: ApiClient) {}

  getConfig(type: ArrType): Promise<Response> {
    return this.client.get(`/api/configuration/${type}`);
  }

  updateConfig(type: ArrType, body: Record<string, unknown>): Promise<Response> {
    return this.client.put(`/api/configuration/${type}`, body);
  }

  createInstance(type: ArrType, instance: ArrInstancePayload): Promise<Response> {
    return this.client.post(`/api/configuration/${type}/instances`, instance);
  }

  updateInstance(type: ArrType, id: string, instance: ArrInstancePayload): Promise<Response> {
    return this.client.put(`/api/configuration/${type}/instances/${id}`, instance);
  }

  deleteInstance(type: ArrType, id: string): Promise<Response> {
    return this.client.delete(`/api/configuration/${type}/instances/${id}`);
  }

  testInstance(type: ArrType, instance: ArrInstancePayload): Promise<Response> {
    return this.client.post(`/api/configuration/${type}/instances/test`, instance);
  }
}
