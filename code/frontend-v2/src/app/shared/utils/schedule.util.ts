import { ScheduleUnit } from '@shared/models/enums';
import { JobSchedule } from '@shared/models/queue-cleaner-config.model';

export function generateCronExpression(schedule: JobSchedule): string {
  const { every, type } = schedule;
  switch (type) {
    case ScheduleUnit.Seconds:
      return `0/${every} * * ? * * *`;
    case ScheduleUnit.Minutes:
      return `0 0/${every} * ? * * *`;
    case ScheduleUnit.Hours:
      return `0 0 0/${every} ? * * *`;
    default:
      return `0 0/${every} * ? * * *`;
  }
}

export function parseCronToJobSchedule(cronExpression: string): JobSchedule | undefined {
  try {
    const parts = cronExpression.split(' ');

    // Handle both 6-part and 7-part cron expressions
    if (parts.length !== 6 && parts.length !== 7) return undefined;

    // Every n seconds — handle both */n and 0/n formats
    if ((parts[0].startsWith('*/') || parts[0].startsWith('0/')) && parts[1] === '*') {
      const seconds = parseInt(parts[0].substring(2));
      if (!isNaN(seconds) && seconds > 0 && seconds < 60) {
        return { every: seconds, type: ScheduleUnit.Seconds };
      }
    }

    // Every n minutes — handle both */n and 0/n formats
    if (parts[0] === '0' && (parts[1].startsWith('*/') || parts[1].startsWith('0/'))) {
      const minutes = parseInt(parts[1].substring(2));
      if (!isNaN(minutes) && minutes > 0 && minutes < 60) {
        return { every: minutes, type: ScheduleUnit.Minutes };
      }
    }

    // Every n hours — handle both */n and 0/n formats
    if (parts[0] === '0' && parts[1] === '0' && (parts[2].startsWith('*/') || parts[2].startsWith('0/'))) {
      const hours = parseInt(parts[2].substring(2));
      if (!isNaN(hours) && hours > 0 && hours < 24) {
        return { every: hours, type: ScheduleUnit.Hours };
      }
    }
  } catch {
    // Couldn't parse — fall through
  }

  return undefined;
}
