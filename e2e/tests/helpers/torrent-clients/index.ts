import { QBittorrentDriver } from './qbittorrent';
import { TransmissionDriver } from './transmission';
import { DelugeDriver } from './deluge';
import { RTorrentDriver } from './rtorrent';
import { UTorrentDriver } from './utorrent';
import { TorrentClientDriver, TorrentClientType } from './types';

export { TorrentClientDriver, TorrentClientType };
export { ClientNotImplementedError } from './types';

export interface TorrentClientFixture {
  driver: TorrentClientDriver;
  /** Whether the spec should actually run against this driver. */
  enabled: boolean;
  /** Reason this client is disabled (shown in test.skip). */
  skipReason?: string;
}

export const ALL_CLIENTS: TorrentClientFixture[] = [
  { driver: new QBittorrentDriver(), enabled: true },
  { driver: new TransmissionDriver(), enabled: true },
  { driver: new DelugeDriver(), enabled: true },
  { driver: new UTorrentDriver(), enabled: true },
  { driver: new RTorrentDriver(), enabled: true },
];
