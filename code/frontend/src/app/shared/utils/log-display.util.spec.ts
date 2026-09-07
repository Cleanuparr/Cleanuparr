import { LogEventLevel } from '@shared/models/enums';
import { logIcon, logLevelLabel, logSeverity } from './log-display.util';

describe('logSeverity', () => {
  it.each([
    [LogEventLevel.Error, 'error'],
    [LogEventLevel.Fatal, 'error'],
    ['critical', 'error'],
    [LogEventLevel.Warning, 'warning'],
    [LogEventLevel.Information, 'info'],
    ['info', 'info'],
    [LogEventLevel.Debug, 'success'],
    [LogEventLevel.Verbose, 'success'],
    ['trace', 'success'],
  ])('maps %s to %s', (level, expected) => {
    expect(logSeverity(level)).toBe(expected);
  });

  it('matches regardless of casing', () => {
    expect(logSeverity('ERROR')).toBe('error');
  });

  it('falls back to default for an unknown level', () => {
    expect(logSeverity('Silly')).toBe('default');
  });
});

describe('logIcon', () => {
  it.each([
    [LogEventLevel.Error, 'tablerCircleX'],
    [LogEventLevel.Fatal, 'tablerCircleX'],
    [LogEventLevel.Warning, 'tablerAlertTriangle'],
    [LogEventLevel.Information, 'tablerInfoCircle'],
    [LogEventLevel.Debug, 'tablerCode'],
    [LogEventLevel.Verbose, 'tablerCode'],
    ['Silly', 'tablerCircle'],
  ])('maps %s to %s', (level, expected) => {
    expect(logIcon(level)).toBe(expected);
  });
});

describe('logLevelLabel', () => {
  it('shortens Information', () => {
    expect(logLevelLabel(LogEventLevel.Information)).toBe('Info');
    expect(logLevelLabel('information')).toBe('Info');
  });

  it('capitalises everything else', () => {
    expect(logLevelLabel('WARNING')).toBe('Warning');
    expect(logLevelLabel(LogEventLevel.Verbose)).toBe('Verbose');
  });
});
