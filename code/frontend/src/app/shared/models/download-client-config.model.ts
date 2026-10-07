import { DownloadClientAuthField, DownloadClientCapability, DownloadClientType, DownloadClientTypeName } from './enums';

export interface ClientConfig {
  enabled: boolean;
  id: string;
  name: string;
  type: DownloadClientType;
  typeName: DownloadClientTypeName;
  host: string;
  username: string;
  password?: string;
  apiKey?: string;
  urlBase: string;
  externalUrl?: string;
  downloadDirectorySource?: string | null;
  downloadDirectoryTarget?: string | null;
}

export interface DownloadClientConfig {
  clients: ClientConfig[];
}

export interface CreateDownloadClientDto {
  enabled: boolean;
  name: string;
  typeName: DownloadClientTypeName;
  host?: string;
  username?: string;
  password?: string;
  apiKey?: string;
  urlBase?: string;
  externalUrl?: string;
  downloadDirectorySource?: string | null;
  downloadDirectoryTarget?: string | null;
}

export interface UpdateDownloadClientDto {
  enabled: boolean;
  name: string;
  typeName: DownloadClientTypeName;
  host?: string;
  username?: string;
  password?: string;
  apiKey?: string;
  urlBase?: string;
  externalUrl?: string;
  downloadDirectorySource?: string | null;
  downloadDirectoryTarget?: string | null;
}

export interface TestDownloadClientRequest {
  typeName: DownloadClientTypeName;
  host?: string;
  username?: string;
  password?: string;
  apiKey?: string;
  urlBase?: string;
  clientId?: string;
}

export interface TestConnectionResult {
  message: string;
  responseTime?: number;
}

/** What a download client type's connection form needs and what its service can do. */
export interface DownloadClientTypeInfo {
  typeName: DownloadClientTypeName;
  authFields: DownloadClientAuthField[];
  capabilities: DownloadClientCapability[];
}

export interface DownloadClientTypesResponse {
  types: DownloadClientTypeInfo[];
}
