import { StrikeType } from '@shared/models/enums';
import { formatStrikeType, strikeTypeSeverity } from './strike-display.util';

describe('strikeTypeSeverity', () => {
  it.each([
    [StrikeType.FailedImport, 'error'],
    [StrikeType.Stalled, 'warning'],
    [StrikeType.SlowSpeed, 'info'],
    [StrikeType.SlowTime, 'info'],
    [StrikeType.DownloadingMetadata, 'default'],
    [StrikeType.DeadTorrent, 'default'],
  ])('maps %s to %s', (type, expected) => {
    expect(strikeTypeSeverity(type)).toBe(expected);
  });

  it('matches regardless of casing', () => {
    expect(strikeTypeSeverity('failedimport')).toBe('error');
  });

  it('falls back to default for an unknown type', () => {
    expect(strikeTypeSeverity('Whatever')).toBe('default');
  });
});

describe('formatStrikeType', () => {
  it('splits pascal case into words', () => {
    expect(formatStrikeType(StrikeType.DownloadingMetadata)).toBe('Downloading Metadata');
  });
});
