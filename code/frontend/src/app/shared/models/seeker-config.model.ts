import { SelectionStrategy } from './enums';

export interface SeekerConfig {
  searchEnabled: boolean;
  searchInterval: number;
  proactiveSearchEnabled: boolean;
  selectionStrategy: SelectionStrategy;
  monitoredOnly: boolean;
  useCutoff: boolean;
  useCustomFormatScore: boolean;
  useRoundRobin: boolean;
  instances: SeekerInstanceConfig[];
}

export interface SeekerInstanceConfig {
  arrInstanceId: string;
  instanceName: string;
  instanceType: string;
  enabled: boolean;
  skipTags: string[];
  lastProcessedAt?: string;
  arrInstanceEnabled: boolean;
}

export interface UpdateSeekerConfig {
  searchEnabled: boolean;
  searchInterval: number;
  proactiveSearchEnabled: boolean;
  selectionStrategy: SelectionStrategy;
  monitoredOnly: boolean;
  useCutoff: boolean;
  useCustomFormatScore: boolean;
  useRoundRobin: boolean;
  instances: UpdateSeekerInstanceConfig[];
}

export interface UpdateSeekerInstanceConfig {
  arrInstanceId: string;
  enabled: boolean;
  skipTags: string[];
}
