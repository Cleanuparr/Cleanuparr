export interface ArrInstance {
  id?: string;
  enabled: boolean;
  name: string;
  url: string;
  externalUrl?: string;
  apiKey: string;
  version: number;
}

export interface ArrConfig {
  failedImportMaxStrikes: number;
  instances: ArrInstance[];
}

export interface CreateArrInstanceDto {
  enabled: boolean;
  name: string;
  url: string;
  externalUrl?: string;
  apiKey: string;
  version: number;
}

export interface TestArrInstanceRequest {
  url: string;
  apiKey: string;
  version: number;
}
