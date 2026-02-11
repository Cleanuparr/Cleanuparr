import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  DownloadClientConfig,
  ClientConfig,
  CreateDownloadClientDto,
  TestDownloadClientRequest,
  TestConnectionResult,
} from '@shared/models/download-client-config.model';

@Injectable({ providedIn: 'root' })
export class DownloadClientApi {
  private http = inject(HttpClient);

  getConfig(): Observable<DownloadClientConfig> {
    return this.http.get<DownloadClientConfig>('/api/configuration/download_client');
  }

  create(client: CreateDownloadClientDto): Observable<ClientConfig> {
    return this.http.post<ClientConfig>('/api/configuration/download_client', client);
  }

  update(id: string, client: ClientConfig): Observable<ClientConfig> {
    return this.http.put<ClientConfig>(`/api/configuration/download_client/${id}`, client);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/configuration/download_client/${id}`);
  }

  test(request: TestDownloadClientRequest): Observable<TestConnectionResult> {
    return this.http.post<TestConnectionResult>('/api/configuration/download_client/test', request);
  }
}
