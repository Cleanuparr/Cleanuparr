import { ApiClient } from './client';

/**
 * Backend-side state reset, exposed only when the app runs with
 * <c>Cleanuparr:E2eMode=true</c> (or <c>CLEANUPARR_E2E_MODE=true</c>).
 * Returns 404 in normal deployments.
 */
export class TestResetApi {
  constructor(private readonly client: ApiClient) {}

  reset(): Promise<Response> {
    return this.client.post('/api/__test__/reset');
  }

  resetUsers(): Promise<Response> {
    return this.client.post('/api/__test__/reset/users');
  }

  async resetOrThrow(): Promise<void> {
    const res = await this.reset();
    if (!res.ok) {
      throw new Error(`Test reset failed: ${res.status} ${await res.text()}. Is the backend running with Cleanuparr:E2eMode=true?`);
    }
  }
}
