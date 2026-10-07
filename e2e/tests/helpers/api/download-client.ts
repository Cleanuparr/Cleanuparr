import { ApiClient } from './client';

export type DownloadClientType =
  | 'qbittorrent'
  | 'transmission'
  | 'deluge'
  | 'utorrent'
  | 'rtorrent'
  | 'sabnzbd';

export interface DownloadClientPayload {
  name: string;
  /** Backend type-name enum value (qBittorrent / Deluge / Transmission / uTorrent / rTorrent). */
  typeName?: string;
  /** Full URL including scheme + port. */
  host: string;
  username?: string;
  password?: string;
  apiKey?: string;
  urlBase?: string;
  externalUrl?: string;
  enabled?: boolean;
  /** Existing client id, for test-connection requests that resolve a masked secret. */
  clientId?: string;
}

const TYPE_NAME_MAP: Record<DownloadClientType, string> = {
  qbittorrent: 'qBittorrent',
  transmission: 'Transmission',
  deluge: 'Deluge',
  utorrent: 'uTorrent',
  rtorrent: 'rTorrent',
  sabnzbd: 'Sabnzbd',
};

export function buildDownloadClientPayload(
  type: DownloadClientType,
  overrides: Partial<DownloadClientPayload> & { host: string; name: string },
): DownloadClientPayload {
  return {
    typeName: TYPE_NAME_MAP[type],
    enabled: true,
    ...overrides,
  };
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
