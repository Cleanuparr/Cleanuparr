import { DownloadClientType, DownloadClientTypeName } from './enums';

/**
 * Represents a download client configuration object
 */
export interface DownloadClientConfig {
  /**
   * Collection of download clients configured for the application
   */
  clients: ClientConfig[];
}

/**
 * Represents an individual download client configuration
 */
export interface ClientConfig {
  /**
   * Whether this client is enabled
   */
  enabled: boolean;
  
  /**
   * Unique identifier for this client
   */
  id: string;
  
  /**
   * Friendly name for this client
   */
  name: string;
  
  /**
   * Type of download client (frontend enum)
   */
  type: DownloadClientType;
  
  /**
   * Type name of download client (backend enum)
   */
  typeName: DownloadClientTypeName;
  
  /**
   * Host address for the download client
   */
  host: string;
  
  /**
   * Username for authentication
   */
  username: string;
  
  /**
   * Password for authentication (only included in update)
   */
  password?: string;
  
  /**
   * The base URL path component, used by clients like Transmission and Deluge
   */
  urlBase: string;
}

/**
 * DTO for creating a new download client (without ID)
 */
export interface CreateDownloadClientDto {
  /**
   * Whether this client is enabled
   */
  enabled: boolean;
  
  /**
   * Friendly name for this client
   */
  name: string;

  /**
   * Type of download client (backend enum)
   */
  type: DownloadClientType;
  
  /**
   * Type name of download client (backend enum)
   */
  typeName: DownloadClientTypeName;
  
  /**
   * Host address for the download client
   */
  host?: string;
  
  /**
   * Username for authentication
   */
  username?: string;
  
  /**
   * Password for authentication
   */
  password?: string;
  
  /**
   * The base URL path component, used by clients like Transmission and Deluge
   */
  urlBase?: string;
}

/**
 * Update DTO model for download client configuration
 */
export interface DownloadClientConfigUpdateDto extends DownloadClientConfig {
  /**
   * Clients with potentially sensitive data for updates
   */
  clients: ClientConfigUpdateDto[];
}

/**
 * Update DTO for individual client (includes password)
 */
export interface ClientConfigUpdateDto extends ClientConfig {
  /**
   * Password for authentication (required for updates)
   */
  password: string;
}
