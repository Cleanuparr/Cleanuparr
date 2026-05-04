import { TorrentPrivacyType } from './enums';

export interface QueueRule {
  id?: string;
  name: string;
  enabled: boolean;
  maxStrikes: number;
  privacyType: TorrentPrivacyType;
  minCompletionPercentage: number;
  maxCompletionPercentage: number;
  deletePrivateTorrentsFromClient: boolean;
  changeCategory: boolean;
}

export interface StallRule extends QueueRule {
  resetStrikesOnProgress: boolean;
  minimumProgress?: string | null;
}

export interface SlowRule extends QueueRule {
  resetStrikesOnProgress: boolean;
  minSpeed: string;
  maxTimeHours: number;
  ignoreAboveSize?: string;
}

export interface CreateStallRuleDto {
  name: string;
  enabled: boolean;
  maxStrikes: number;
  privacyType: TorrentPrivacyType;
  minCompletionPercentage: number;
  maxCompletionPercentage: number;
  resetStrikesOnProgress: boolean;
  deletePrivateTorrentsFromClient: boolean;
  changeCategory: boolean;
  minimumProgress?: string | null;
}

export interface CreateSlowRuleDto {
  name: string;
  enabled: boolean;
  maxStrikes: number;
  privacyType: TorrentPrivacyType;
  minCompletionPercentage: number;
  maxCompletionPercentage: number;
  resetStrikesOnProgress: boolean;
  minSpeed: string;
  maxTimeHours: number;
  ignoreAboveSize?: string;
  deletePrivateTorrentsFromClient: boolean;
  changeCategory: boolean;
}
