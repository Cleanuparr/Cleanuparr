import { DownloadClientType, DownloadClientTypeName } from './enums';

export interface ClientConfig {
  enabled: boolean;
  id: string;
  name: string;
  type: DownloadClientType;
  typeName: DownloadClientTypeName;
  host: string;
  username: string;
  password?: string;
  urlBase: string;
  externalUrl?: string;
}

export interface DownloadClientConfig {
  clients: ClientConfig[];
}

export interface CreateDownloadClientDto {
  enabled: boolean;
  name: string;
  type: DownloadClientType;
  typeName: DownloadClientTypeName;
  host?: string;
  username?: string;
  password?: string;
  urlBase?: string;
  externalUrl?: string;
}

export interface TestDownloadClientRequest {
  typeName: DownloadClientTypeName;
  type: DownloadClientType;
  host?: string;
  username?: string;
  password?: string;
  urlBase?: string;
}

export interface TestConnectionResult {
  message: string;
  responseTime?: number;
}
