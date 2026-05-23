export interface OrphanedFilesClientConfig {
  enabled: boolean;
  scanDirectories: string[];
  orphanedDirectory?: string;
}

export interface ClientOrphanedFilesConfig {
  downloadClientId: string;
  downloadClientName: string;
  downloadClientEnabled: boolean;
  clientConfig: OrphanedFilesClientConfig | null;
}

export interface OrphanedFilesCleanerConfig {
  excludePatterns: string[];
  minFileAgeMinutes: number;
  emptyAfterXDays?: number;
  clients: ClientOrphanedFilesConfig[];
}

export function createDefaultClientConfig(): OrphanedFilesClientConfig {
  return {
    enabled: false,
    scanDirectories: [],
  };
}
