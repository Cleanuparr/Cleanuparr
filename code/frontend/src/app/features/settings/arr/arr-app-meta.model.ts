import type { SelectOption } from '@ui';

export interface ArrAppMeta {
  displayName: string;
  versionOptions: SelectOption[];
  defaultPort: number;
  experimental: boolean;
  iconSrc: string;
  featureId?: string;
}
