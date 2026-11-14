import { type SelectOption } from '@ui';
import { QueueRuleContentType, TorrentPrivacyType } from '@shared/models/enums';

export const PRIVACY_TYPE_OPTIONS: SelectOption[] = [
  { label: 'Public', value: TorrentPrivacyType.Public },
  { label: 'Private', value: TorrentPrivacyType.Private },
  { label: 'Both', value: TorrentPrivacyType.Both },
];

export const CONTENT_TYPE_OPTIONS: SelectOption[] = [
  { label: 'All', value: QueueRuleContentType.All },
  { label: 'New', value: QueueRuleContentType.New },
  { label: 'Upgrade', value: QueueRuleContentType.Upgrade },
];
