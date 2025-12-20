/**
 * Represents an *arr instance with connection details
 */
export interface ArrInstance {
  id?: string;
  enabled: boolean;
  name: string;
  url: string;
  apiKey: string;
}

/**
 * DTO for creating new Arr instances without requiring an ID
 */
export interface CreateArrInstanceDto {
  enabled: boolean;
  name: string;
  url: string;
  apiKey: string;
}

/**
 * Request for testing an Arr instance connection
 */
export interface TestArrInstanceRequest {
  url: string;
  apiKey: string;
}
