import type { BadgeSeverity } from '@ui/badge/badge.component';
import { StrikeType } from '@shared/models/enums';
import { formatEnumValue, matchEnum } from './enum.util';

export function strikeTypeSeverity(type: string): BadgeSeverity {
  switch (matchEnum(StrikeType, type)) {
    case StrikeType.FailedImport:
      return 'error';
    case StrikeType.Stalled:
      return 'warning';
    case StrikeType.SlowSpeed:
    case StrikeType.SlowTime:
      return 'info';
    default:
      return 'default';
  }
}

export function formatStrikeType(type: string): string {
  return formatEnumValue(type);
}
