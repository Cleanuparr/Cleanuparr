import type { BadgeSeverity } from '@ui/badge/badge.component';
import { InstanceType } from '@shared/models/enums';

export function instanceTypeSeverity(type: string): BadgeSeverity {
  switch (type) {
    case InstanceType.Radarr:
      return 'warning';
    case InstanceType.Sonarr:
      return 'info';
    default:
      return 'default';
  }
}

/** Highlights the instance types the seeker stats tabs report on. */
export function instanceTypeHighlight(type: string): BadgeSeverity {
  switch (type) {
    case InstanceType.Radarr:
    case InstanceType.Sonarr:
      return 'info';
    default:
      return 'default';
  }
}
