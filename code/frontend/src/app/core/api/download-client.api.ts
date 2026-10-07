import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DownloadClientConfig,
  ClientConfig,
  CreateDownloadClientDto,
  UpdateDownloadClientDto,
  TestDownloadClientRequest,
  TestConnectionResult,
  DownloadClientTypeInfo,
  DownloadClientTypesResponse,
} from '@shared/models/download-client-config.model';
import { DownloadClientTypeName } from '@shared/models/enums';

/** Indexes a `/download_client/types` response by type name for O(1) lookups. */
export function indexClientTypes(
  response: DownloadClientTypesResponse,
): Partial<Record<DownloadClientTypeName, DownloadClientTypeInfo>> {
  const result: Partial<Record<DownloadClientTypeName, DownloadClientTypeInfo>> = {};
  for (const type of response.types) {
    result[type.typeName] = type;
  }
  return result;
}

@Injectable({ providedIn: 'root' })
export class DownloadClientApi {
  private http = inject(HttpClient);

  getConfig(): Observable<DownloadClientConfig> {
    return this.http.get<DownloadClientConfig>('/api/configuration/download_client');
  }

  create(client: CreateDownloadClientDto): Observable<ClientConfig> {
    return this.http.post<ClientConfig>('/api/configuration/download_client', client);
  }

  update(id: string, client: UpdateDownloadClientDto): Observable<ClientConfig> {
    return this.http.put<ClientConfig>(`/api/configuration/download_client/${id}`, client);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/configuration/download_client/${id}`);
  }

  test(request: TestDownloadClientRequest): Observable<TestConnectionResult> {
    return this.http.post<TestConnectionResult>('/api/configuration/download_client/test', request);
  }

  getTypes(): Observable<DownloadClientTypesResponse> {
    return this.http.get<DownloadClientTypesResponse>('/api/configuration/download_client/types');
  }
}
