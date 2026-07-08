import { ApiClient } from './client';

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface LoginResponse {
  requiresTwoFactor: boolean;
  loginToken?: string;
  tokens?: TokenResponse;
}

export class AuthApi {
  constructor(private readonly client: ApiClient) {}

  status(): Promise<Response> {
    return this.client.get('/api/auth/status');
  }

  setupAccount(username: string, password: string): Promise<Response> {
    return this.client.post('/api/auth/setup/account', { username, password });
  }

  setupGenerate2fa(): Promise<Response> {
    return this.client.post('/api/auth/setup/2fa/generate');
  }

  setupVerify2fa(token: string): Promise<Response> {
    return this.client.post('/api/auth/setup/2fa/verify', { token });
  }

  setupComplete(): Promise<Response> {
    return this.client.post('/api/auth/setup/complete');
  }

  login(username: string, password: string): Promise<Response> {
    return this.client.post('/api/auth/login', { username, password });
  }

  loginTwoFactor(loginToken: string, code: string): Promise<Response> {
    return this.client.post('/api/auth/login/2fa', { loginToken, code });
  }

  refresh(refreshToken: string): Promise<Response> {
    return this.client.post('/api/auth/refresh', { refreshToken });
  }

  logout(refreshToken: string): Promise<Response> {
    return this.client.post('/api/auth/logout', { refreshToken });
  }

  oidcStart(): Promise<Response> {
    return this.client.post('/api/auth/oidc/start');
  }

  oidcExchange(oneTimeCode: string): Promise<Response> {
    return this.client.post('/api/auth/oidc/exchange', { code: oneTimeCode });
  }

  plexPin(): Promise<Response> {
    return this.client.post('/api/auth/login/plex/pin');
  }

  plexVerify(pinId: string): Promise<Response> {
    return this.client.post('/api/auth/login/plex/verify', { pinId });
  }

  async loginAndCaptureTokens(username: string, password: string): Promise<TokenResponse> {
    const res = await this.login(username, password);
    if (!res.ok) {
      throw new Error(`Login failed: ${res.status} ${await res.text()}`);
    }
    const data: LoginResponse = await res.json();
    if (data.requiresTwoFactor || !data.tokens) {
      throw new Error('Unexpected 2FA requirement in admin login flow');
    }
    return data.tokens;
  }
}
