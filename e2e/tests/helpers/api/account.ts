import { ApiClient } from './client';

export class AccountApi {
  constructor(private readonly client: ApiClient) {}

  get(): Promise<Response> {
    return this.client.get('/api/account');
  }

  changePassword(currentPassword: string, newPassword: string): Promise<Response> {
    return this.client.put('/api/account/password', { currentPassword, newPassword });
  }

  generate2fa(): Promise<Response> {
    return this.client.post('/api/account/2fa/regenerate');
  }

  enable2fa(password: string): Promise<Response> {
    return this.client.post('/api/account/2fa/enable', { password });
  }

  enable2faVerify(token: string): Promise<Response> {
    return this.client.post('/api/account/2fa/enable/verify', { token });
  }

  disable2fa(password: string, code: string): Promise<Response> {
    return this.client.post('/api/account/2fa/disable', { password, code });
  }

  getApiKey(): Promise<Response> {
    return this.client.get('/api/account/api-key');
  }

  regenerateApiKey(): Promise<Response> {
    return this.client.post('/api/account/api-key/regenerate');
  }

  linkPlex(): Promise<Response> {
    return this.client.post('/api/account/plex/link');
  }

  verifyPlexLink(pinId: string): Promise<Response> {
    return this.client.post('/api/account/plex/link/verify', { pinId });
  }

  unlinkPlex(): Promise<Response> {
    return this.client.delete('/api/account/plex/link');
  }

  getOidcConfig(): Promise<Response> {
    return this.client.get('/api/account/oidc');
  }

  updateOidcConfig(config: Record<string, unknown>): Promise<Response> {
    return this.client.put('/api/account/oidc', config);
  }

  startOidcLink(): Promise<Response> {
    return this.client.post('/api/account/oidc/link');
  }

  unlinkOidc(): Promise<Response> {
    return this.client.delete('/api/account/oidc/link');
  }

  async patchOidcConfig(updates: Record<string, unknown>): Promise<void> {
    const current = await (await this.getOidcConfig()).json();
    const res = await this.updateOidcConfig({ ...current, ...updates });
    if (!res.ok) {
      throw new Error(`Failed to patch OIDC config: ${res.status} ${await res.text()}`);
    }
  }
}
