import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { JobType } from '@shared/models/enums';

@Injectable({ providedIn: 'root' })
export class JobsApi {
  private http = inject(HttpClient);

  trigger(jobType: JobType): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`/api/jobs/${jobType}/trigger`, {});
  }
}
