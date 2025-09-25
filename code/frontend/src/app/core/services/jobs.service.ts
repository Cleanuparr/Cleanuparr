import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ApplicationPathService } from './base-path.service';
import { JobInfo, JobType } from '../models/job.models';

@Injectable({
  providedIn: 'root'
})
export class JobsService {
  private http = inject(HttpClient);
  private pathService = inject(ApplicationPathService);
  
  private get baseUrl(): string {
    return this.pathService.buildApiUrl('/jobs');
  }

  /**
   * Get all jobs information
   */
  getAllJobs(): Observable<JobInfo[]> {
    return this.http.get<JobInfo[]>(this.baseUrl)
      .pipe(catchError(this.handleError));
  }

  /**
   * Get specific job information
   */
  getJob(jobType: JobType): Observable<JobInfo> {
    return this.http.get<JobInfo>(`${this.baseUrl}/${jobType}`)
      .pipe(catchError(this.handleError));
  }

  /**
   * Start a job with optional schedule
   */
  startJob(jobType: JobType, schedule?: any): Observable<any> {
    const body = schedule ? { schedule } : {};
    return this.http.post(`${this.baseUrl}/${jobType}/start`, body)
      .pipe(catchError(this.handleError));
  }

  /**
   * Trigger a job for one-time execution
   */
  triggerJob(jobType: JobType): Observable<any> {
    return this.http.post(`${this.baseUrl}/${jobType}/trigger`, {})
      .pipe(catchError(this.handleError));
  }

  /**
   * Update job schedule
   */
  updateJobSchedule(jobType: JobType, schedule: any): Observable<any> {
    return this.http.put(`${this.baseUrl}/${jobType}/schedule`, { schedule })
      .pipe(catchError(this.handleError));
  }

  private handleError(error: any): Observable<never> {
    console.error('Jobs service error:', error);
    return throwError(() => error);
  }
}