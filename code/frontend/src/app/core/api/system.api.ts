import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface SystemStatus {
  application: {
    version: string;
    startTime: string;
    upTime: string;
    memoryUsageMB: number;
    processorTime: string;
  };
  downloadClient: unknown;
  mediaManagers: {
    sonarr: { instanceCount: number };
    radarr: { instanceCount: number };
    lidarr: { instanceCount: number };
    readarr: { instanceCount: number };
  };
}

export interface HealthCheckResult {
  id: string;
  name: string;
  type: string;
  host: string;
  enabled: boolean;
  isConnected: boolean;
}

@Injectable({ providedIn: 'root' })
export class SystemApi {
  private http = inject(HttpClient);

  getStatus(): Observable<SystemStatus> {
    return this.http.get<SystemStatus>('/api/status');
  }

  getDownloadClientStatus(): Observable<{ clients: HealthCheckResult[] }> {
    return this.http.get<{ clients: HealthCheckResult[] }>('/api/status/download-client');
  }

  getArrStatus(): Observable<unknown> {
    return this.http.get('/api/status/arrs');
  }

  getHealth(): Observable<{ status: string; timestamp: string }> {
    return this.http.get<{ status: string; timestamp: string }>('/health');
  }

  checkAllHealth(): Observable<HealthCheckResult[]> {
    return this.http.post<HealthCheckResult[]>('/api/health/check', {});
  }
}
