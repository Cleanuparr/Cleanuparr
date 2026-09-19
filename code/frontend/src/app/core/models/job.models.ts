import { JobType } from '@shared/models/enums';

export { JobType };

export interface JobInfo {
  name: string;
  status: string;
  schedule: string;
  nextRunTime?: Date;
  previousRunTime?: Date;
  jobType: string;
}
