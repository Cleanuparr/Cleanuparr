import { SelectionStrategy, SonarrSearchMode } from './enums';

export interface SeekerConfig {
  searchEnabled: boolean;
  searchInterval: number;
  proactiveSearchEnabled: boolean;
  selectionStrategy: SelectionStrategy;
  useRoundRobin: boolean;
  postReleaseGraceHours: number;
  quietHoursEnabled: boolean;
  quietHoursStart: string;
  quietHoursEnd: string;
  instances: SeekerInstanceConfig[];
}

export interface SeekerInstanceConfig {
  arrInstanceId: string;
  instanceName: string;
  instanceType: string;
  enabled: boolean;
  skipTags: string[];
  includeTags: string[];
  lastProcessedAt?: string;
  arrInstanceEnabled: boolean;
  activeDownloadLimit: number;
  ignoreStruckDownloads: boolean;
  recentGrabWindowHours: number;
  restUnitsEnabled: boolean;
  restMaxDays: number;
  minCycleTimeDays: number;
  monitoredOnly: boolean;
  useCutoff: boolean;
  useCustomFormatScore: boolean;
  sonarrSearchMode: SonarrSearchMode;
}

export interface UpdateSeekerConfig {
  searchEnabled: boolean;
  searchInterval: number;
  proactiveSearchEnabled: boolean;
  selectionStrategy: SelectionStrategy;
  useRoundRobin: boolean;
  postReleaseGraceHours: number;
  quietHoursEnabled: boolean;
  quietHoursStart: string;
  quietHoursEnd: string;
  instances: UpdateSeekerInstanceConfig[];
}

export interface UpdateSeekerInstanceConfig {
  arrInstanceId: string;
  enabled: boolean;
  skipTags: string[];
  includeTags: string[];
  activeDownloadLimit: number;
  ignoreStruckDownloads: boolean;
  recentGrabWindowHours: number;
  restUnitsEnabled: boolean;
  restMaxDays: number;
  minCycleTimeDays: number;
  monitoredOnly: boolean;
  useCutoff: boolean;
  useCustomFormatScore: boolean;
  sonarrSearchMode: SonarrSearchMode;
}
