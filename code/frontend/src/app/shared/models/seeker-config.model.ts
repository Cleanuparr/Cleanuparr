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
  postReleaseGraceHours: number;
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
  activeDownloadLimit: number;
  minCycleTimeDays: number;
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
  postReleaseGraceHours: number;
  instances: UpdateSeekerInstanceConfig[];
}

export interface UpdateSeekerInstanceConfig {
  arrInstanceId: string;
  enabled: boolean;
  skipTags: string[];
  activeDownloadLimit: number;
  minCycleTimeDays: number;
}
