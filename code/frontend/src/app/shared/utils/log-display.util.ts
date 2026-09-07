import type { BadgeSeverity } from '@ui/badge/badge.component';
import { LogEventLevel } from '@shared/models/enums';
import { matchEnum } from './enum.util';

export function logSeverity(level: string): BadgeSeverity {
  switch (matchEnum(LogEventLevel, level)) {
    case LogEventLevel.Error:
    case LogEventLevel.Fatal:
      return 'error';
    case LogEventLevel.Warning:
      return 'warning';
    case LogEventLevel.Information:
      return 'info';
    case LogEventLevel.Debug:
    case LogEventLevel.Verbose:
      return 'success';
    default:
      return 'default';
  }
}

export function logIcon(level: string): string {
  switch (matchEnum(LogEventLevel, level)) {
    case LogEventLevel.Error:
    case LogEventLevel.Fatal:
      return 'tablerCircleX';
    case LogEventLevel.Warning:
      return 'tablerAlertTriangle';
    case LogEventLevel.Information:
      return 'tablerInfoCircle';
    case LogEventLevel.Debug:
    case LogEventLevel.Verbose:
      return 'tablerCode';
    default:
      return 'tablerCircle';
  }
}

export function logLevelLabel(level: string): string {
  if (matchEnum(LogEventLevel, level) === LogEventLevel.Information) {
    return 'Info';
  }
  return level.charAt(0).toUpperCase() + level.slice(1).toLowerCase();
}
