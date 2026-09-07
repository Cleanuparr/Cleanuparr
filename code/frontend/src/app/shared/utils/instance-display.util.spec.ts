import { InstanceType } from '@shared/models/enums';
import { instanceTypeHighlight, instanceTypeSeverity } from './instance-display.util';

describe('instanceTypeSeverity', () => {
  it.each([
    [InstanceType.Radarr, 'warning'],
    [InstanceType.Sonarr, 'info'],
    [InstanceType.Lidarr, 'default'],
    [InstanceType.LazyLibrarian, 'default'],
  ])('maps %s to %s', (type, expected) => {
    expect(instanceTypeSeverity(type)).toBe(expected);
  });

  it('falls back to default for an unknown type', () => {
    expect(instanceTypeSeverity('Whatever')).toBe('default');
  });
});

describe('instanceTypeHighlight', () => {
  it.each([
    [InstanceType.Radarr, 'info'],
    [InstanceType.Sonarr, 'info'],
    [InstanceType.Whisparr, 'default'],
  ])('maps %s to %s', (type, expected) => {
    expect(instanceTypeHighlight(type)).toBe(expected);
  });
});
