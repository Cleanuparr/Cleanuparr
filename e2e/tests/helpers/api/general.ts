import { ApiClient } from './client';

export interface AuthBypassOptions {
  disableAuthForLocalAddresses: boolean;
  trustForwardedHeaders: boolean;
  trustedNetworks: string[];
}

export class GeneralApi {
  constructor(private readonly client: ApiClient) {}

  getConfig(): Promise<Response> {
    return this.client.get('/api/configuration/general');
  }

  updateConfig(body: Record<string, unknown>): Promise<Response> {
    return this.client.put('/api/configuration/general', body);
  }

  purgeStrikes(): Promise<Response> {
    return this.client.post('/api/configuration/strikes/purge');
  }

  async getJsonConfig(): Promise<Record<string, unknown>> {
    const res = await this.getConfig();
    if (!res.ok) {
      throw new Error(`GET general config failed: ${res.status} ${await res.text()}`);
    }
    return res.json();
  }

  async patch(updates: Record<string, unknown>): Promise<void> {
    const current = await this.getJsonConfig();
    const res = await this.updateConfig({ ...current, ...updates });
    if (!res.ok) {
      throw new Error(`PUT general config failed: ${res.status} ${await res.text()}`);
    }
  }

  async setAuthBypass(opts: AuthBypassOptions): Promise<void> {
    await this.patch({
      auth: {
        disableAuthForLocalAddresses: opts.disableAuthForLocalAddresses,
        trustForwardedHeaders: opts.trustForwardedHeaders,
        trustedNetworks: opts.trustedNetworks,
      },
    });
  }
}
