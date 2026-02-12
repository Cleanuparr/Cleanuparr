import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { ArrType } from '@shared/models/enums';
import {
  ArrConfig,
  ArrInstance,
  CreateArrInstanceDto,
  TestArrInstanceRequest,
} from '@shared/models/arr-config.model';
import { TestConnectionResult } from '@shared/models/download-client-config.model';

@Injectable({ providedIn: 'root' })
export class ArrApi {
  private http = inject(HttpClient);

  getConfig(type: ArrType): Observable<ArrConfig> {
    return this.http.get<ArrConfig>(`/api/configuration/${type}`);
  }

  updateConfig(type: ArrType, config: { failedImportMaxStrikes: number }): Observable<void> {
    return this.http.put<void>(`/api/configuration/${type}`, config);
  }

  createInstance(type: ArrType, instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.post<ArrInstance>(`/api/configuration/${type}/instances`, instance);
  }

  updateInstance(type: ArrType, id: string, instance: CreateArrInstanceDto): Observable<ArrInstance> {
    return this.http.put<ArrInstance>(`/api/configuration/${type}/instances/${id}`, instance);
  }

  deleteInstance(type: ArrType, id: string): Observable<void> {
    return this.http.delete<void>(`/api/configuration/${type}/instances/${id}`);
  }

  testInstance(type: ArrType, request: TestArrInstanceRequest): Observable<TestConnectionResult> {
    return this.http.post<TestConnectionResult>(`/api/configuration/${type}/instances/test`, request);
  }
}
