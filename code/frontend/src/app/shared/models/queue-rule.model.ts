export enum TorrentPrivacyType {
  Public = "Public",
  Private = "Private",
  Both = "Both"
}

export interface PrivacyTypeOption {
  label: string;
  value: TorrentPrivacyType;
}

export interface QueueRule {
  id?: string;
  name: string;
  enabled: boolean;
  maxStrikes: number;
  privacyType: TorrentPrivacyType;
  maxCompletionPercentage: number;
}

export interface StallRule extends QueueRule {
  resetStrikesOnProgress: boolean;
}

export interface SlowRule extends QueueRule {
  resetStrikesOnProgress: boolean;
  minSpeed: string;
  maxTimeHours: number;
  ignoreAboveSize?: string;
}

// DTO interfaces for API operations
export interface CreateStallRuleDto {
  name: string;
  description?: string;
  enabled: boolean;
  maxStrikes: number;
  privacyType: TorrentPrivacyType;
  maxCompletionPercentage: number;
  resetStrikesOnProgress: boolean;
}

export interface UpdateStallRuleDto extends CreateStallRuleDto {
  id: string;
}

export interface CreateSlowRuleDto {
  name: string;
  description?: string;
  enabled: boolean;
  maxStrikes: number;
  privacyType: TorrentPrivacyType;
  maxCompletionPercentage: number;
  resetStrikesOnProgress: boolean;
  minSpeed: string;
  maxTimeHours: number;
  ignoreAboveSize?: string;
}

export interface UpdateSlowRuleDto extends CreateSlowRuleDto {
  id: string;
}