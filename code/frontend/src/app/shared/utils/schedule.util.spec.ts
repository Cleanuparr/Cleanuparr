import { ScheduleUnit } from '@shared/models/enums';
import {
  generateCronExpression,
  parseCronToJobSchedule,
  ScheduleOptions,
} from './schedule.util';

describe('schedule.util', () => {
  it('round-trips every offered schedule option', () => {
    for (const type of Object.keys(ScheduleOptions) as ScheduleUnit[]) {
      for (const every of ScheduleOptions[type]) {
        const cron = generateCronExpression({ every, type });

        expect(parseCronToJobSchedule(cron)).toEqual({ every, type });
      }
    }
  });

  it('parses the */n form identically to the generated 0/n form', () => {
    expect(parseCronToJobSchedule('*/30 * * ? * * *')).toEqual({
      every: 30,
      type: ScheduleUnit.Seconds,
    });
    expect(parseCronToJobSchedule('0 */15 * ? * * *')).toEqual({
      every: 15,
      type: ScheduleUnit.Minutes,
    });
    expect(parseCronToJobSchedule('0 0 */6 ? * * *')).toEqual({
      every: 6,
      type: ScheduleUnit.Hours,
    });
  });

  it('accepts 6-part and 7-part expressions and rejects other lengths', () => {
    expect(parseCronToJobSchedule('0 0/5 * ? * *')).toEqual({
      every: 5,
      type: ScheduleUnit.Minutes,
    });
    expect(parseCronToJobSchedule('0 0/5 * ? * * *')).toEqual({
      every: 5,
      type: ScheduleUnit.Minutes,
    });
    expect(parseCronToJobSchedule('0 0/5 * ? *')).toBeUndefined();
    expect(parseCronToJobSchedule('0 0/5 * ? * * * *')).toBeUndefined();
  });

  it('rejects out-of-range intervals', () => {
    expect(parseCronToJobSchedule('0/0 * * ? * * *')).toBeUndefined();
    expect(parseCronToJobSchedule('0/60 * * ? * * *')).toBeUndefined();
    expect(parseCronToJobSchedule('0 0/60 * ? * * *')).toBeUndefined();
    expect(parseCronToJobSchedule('0 0 0/24 ? * * *')).toBeUndefined();
  });

  it('rejects non-numeric intervals', () => {
    expect(parseCronToJobSchedule('0/abc * * ? * * *')).toBeUndefined();
    expect(parseCronToJobSchedule('0 0/abc * ? * * *')).toBeUndefined();
  });

  it('rejects expressions that match no supported pattern', () => {
    expect(parseCronToJobSchedule('0 30 4 ? * * *')).toBeUndefined();
    expect(parseCronToJobSchedule('')).toBeUndefined();
  });

  it('falls back to the minutes template for an unknown unit', () => {
    expect(generateCronExpression({ every: 5, type: 'Days' as ScheduleUnit })).toBe(
      '0 0/5 * ? * * *',
    );
  });
});
