export interface OrphanedFilesClientConfig {
  enabled: boolean;
  scanDirectories: string[];
  orphanedDirectory?: string | null;
}

export interface ClientOrphanedFilesConfig {
  downloadClientId: string;
  downloadClientName: string;
  downloadClientEnabled: boolean;
  clientConfig: OrphanedFilesClientConfig | null;
}

export interface OrphanedFilesCleanerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  excludePatterns: string[];
  minFileAgeMinutes: number;
  maxOrphanedFilesToProcess: number;
  emptyAfterXDays?: number | null;
  clients: ClientOrphanedFilesConfig[];
}

export function createDefaultClientConfig(): OrphanedFilesClientConfig {
  return {
    enabled: false,
    scanDirectories: [],
    orphanedDirectory: null,
  };
}
