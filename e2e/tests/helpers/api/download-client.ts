import { ApiClient } from './client';

export type DownloadClientType =
  | 'qbittorrent'
  | 'transmission'
  | 'deluge'
  | 'utorrent'
  | 'rtorrent';

export interface DownloadClientPayload {
  name: string;
  type: DownloadClientType;
  host: string;
  port?: number;
  username?: string;
  password?: string;
  useSsl?: boolean;
  urlBase?: string;
  enabled?: boolean;
}

export class DownloadClientApi {
  constructor(private readonly client: ApiClient) {}

  list(): Promise<Response> {
    return this.client.get('/api/configuration/download_client');
  }

  create(body: DownloadClientPayload): Promise<Response> {
    return this.client.post('/api/configuration/download_client', body);
  }

  update(id: string, body: DownloadClientPayload): Promise<Response> {
    return this.client.put(`/api/configuration/download_client/${id}`, body);
  }

  delete(id: string): Promise<Response> {
    return this.client.delete(`/api/configuration/download_client/${id}`);
  }

  test(body: DownloadClientPayload): Promise<Response> {
    return this.client.post('/api/configuration/download_client/test', body);
  }
}
