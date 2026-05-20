export interface OrphanedFilesCleanerConfig {
  enabled: boolean;
  cronExpression: string;
  useAdvancedScheduling: boolean;
  scanDirectories: string[];
  orphanedDirectory?: string;
  downloadDirectorySource?: string;
  downloadDirectoryTarget?: string;
  excludePatterns: string[];
  minFileAgeMinutes: number;
  maxOrphanedFilesToProcess: number;
  emptyAfterXDays?: number | null;
}
