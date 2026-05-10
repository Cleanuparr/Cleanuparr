import { ApiClient } from './client';

export type DownloadClientType =
  | 'qbittorrent'
  | 'transmission'
  | 'deluge'
  | 'utorrent'
  | 'rtorrent';

export type DownloadClientCategory = 'Torrent' | 'Usenet';

export interface DownloadClientPayload {
  name: string;
  /** Backend enum value (Torrent / Usenet). */
  type?: DownloadClientCategory;
  /** Backend type-name enum value (qBittorrent / Deluge / Transmission / uTorrent / rTorrent). */
  typeName?: string;
  /** Full URL including scheme + port. */
  host: string;
  username?: string;
  password?: string;
  urlBase?: string;
  externalUrl?: string;
  enabled?: boolean;
}

const TYPE_NAME_MAP: Record<DownloadClientType, string> = {
  qbittorrent: 'qBittorrent',
  transmission: 'Transmission',
  deluge: 'Deluge',
  utorrent: 'uTorrent',
  rtorrent: 'rTorrent',
};

export function buildDownloadClientPayload(
  type: DownloadClientType,
  overrides: Partial<DownloadClientPayload> & { host: string; name: string },
): DownloadClientPayload {
  return {
    type: 'Torrent',
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
