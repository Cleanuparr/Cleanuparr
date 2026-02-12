import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { JobInfo, JobScheduleRequest } from '@core/models/job.models';
import { JobType } from '@shared/models/enums';

@Injectable({ providedIn: 'root' })
export class JobsApi {
  private http = inject(HttpClient);

  getAll(): Observable<JobInfo[]> {
    return this.http.get<JobInfo[]>('/api/jobs');
  }

  get(jobType: JobType): Observable<JobInfo> {
    return this.http.get<JobInfo>(`/api/jobs/${jobType}`);
  }

  start(jobType: JobType): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`/api/jobs/${jobType}/start`, {});
  }

  trigger(jobType: JobType): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`/api/jobs/${jobType}/trigger`, {});
  }

  updateSchedule(jobType: JobType, schedule: JobScheduleRequest): Observable<{ message: string }> {
    return this.http.put<{ message: string }>(`/api/jobs/${jobType}/schedule`, schedule);
  }
}
