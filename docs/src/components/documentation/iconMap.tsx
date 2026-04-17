import React from 'react';
import {
  IconBolt,
  IconX,
  IconPlayerPause,
  IconGauge,
  IconBan,
  IconSearch,
  IconRadar,
  IconTrendingUp,
  IconTarget,
  IconSeedling,
  IconLinkOff,
  IconLink,
  IconBell,
  IconFilterOff,
  IconBrush,
  IconSparkles,
  IconTrash,
  IconTag,
  IconInbox,
  IconCheck,
  IconBrandDocker,
  IconBrandTelegram,
  IconInfoCircle,
  IconAlertTriangle,
  IconAlertOctagon,
} from '@tabler/icons-react';

const iconMap: Record<string, React.ComponentType<{ size?: number; stroke?: number }>> = {
  'bolt': IconBolt,
  'x': IconX,
  'player-pause': IconPlayerPause,
  'gauge': IconGauge,
  'ban': IconBan,
  'search': IconSearch,
  'radar': IconRadar,
  'trending-up': IconTrendingUp,
  'target': IconTarget,
  'seedling': IconSeedling,
  'link-off': IconLinkOff,
  'link': IconLink,
  'bell': IconBell,
  'filter-off': IconFilterOff,
  'broom': IconBrush,
  'sparkles': IconSparkles,
  'trash': IconTrash,
  'tag': IconTag,
  'inbox': IconInbox,
  'check': IconCheck,
  'brand-docker': IconBrandDocker,
  'brand-telegram': IconBrandTelegram,
  'info-circle': IconInfoCircle,
  'alert-triangle': IconAlertTriangle,
  'alert-octagon': IconAlertOctagon,
};

export function renderIcon(key: string, size: number = 18): React.ReactNode {
  const Icon = iconMap[key];
  if (!Icon) return null;
  return <Icon size={size} stroke={1.5} />;
}
