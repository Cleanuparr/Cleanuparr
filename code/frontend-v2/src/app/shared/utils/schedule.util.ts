import { ScheduleUnit } from '@shared/models/enums';
import { JobSchedule } from '@shared/models/queue-cleaner-config.model';

export function generateCronExpression(schedule: JobSchedule): string {
  const { every, type } = schedule;
  switch (type) {
    case ScheduleUnit.Seconds:
      return `0/${every} * * * * ?`;
    case ScheduleUnit.Minutes:
      return `0 0/${every} * * * ?`;
    case ScheduleUnit.Hours:
      return `0 0 0/${every} * * ?`;
    default:
      return `0 0/${every} * * * ?`;
  }
}
