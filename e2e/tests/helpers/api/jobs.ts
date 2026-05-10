import { ApiClient } from './client';

export type JobType =
  | 'QueueCleaner'
  | 'MalwareBlocker'
  | 'DownloadCleaner'
  | 'BlacklistSync'
  | 'Seeker'
  | 'CustomFormatScoreSyncer';

export class JobsApi {
  constructor(private readonly client: ApiClient) {}

  list(): Promise<Response> {
    return this.client.get('/api/jobs');
  }

  get(jobType: JobType | string): Promise<Response> {
    return this.client.get(`/api/jobs/${jobType}`);
  }

  start(jobType: JobType | string, schedule?: string): Promise<Response> {
    return this.client.post(`/api/jobs/${jobType}/start`, schedule ? { schedule } : undefined);
  }

  trigger(jobType: JobType | string): Promise<Response> {
    return this.client.post(`/api/jobs/${jobType}/trigger`);
  }

  updateSchedule(jobType: JobType | string, schedule: string): Promise<Response> {
    return this.client.put(`/api/jobs/${jobType}/schedule`, { schedule });
  }
}
