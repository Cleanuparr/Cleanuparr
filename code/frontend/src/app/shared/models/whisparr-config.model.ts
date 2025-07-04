/**
 * WhisparrConfig model definitions for the UI
 * These models represent the structures used in the API for Whisparr configuration
 */

import { ArrInstance } from "./arr-config.model";

/**
 * Main WhisparrConfig model representing the configuration for Whisparr integration
 */
export interface WhisparrConfig {
  failedImportMaxStrikes: number;
  instances: ArrInstance[];
} 