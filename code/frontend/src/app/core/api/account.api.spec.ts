import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { Observable, of } from 'rxjs';
import { AccountApi } from './account.api';

type HttpStub = (url: string, body?: unknown) => Observable<null>;

describe('AccountApi', () => {
  function setup() {
    const get = vi.fn<HttpStub>(() => of(null));
    const put = vi.fn<HttpStub>(() => of(null));
    const post = vi.fn<HttpStub>(() => of(null));
    const del = vi.fn<HttpStub>(() => of(null));

    TestBed.configureTestingModule({
      providers: [{ provide: HttpClient, useValue: { get, put, post, delete: del } }],
    });

    return { api: TestBed.inject(AccountApi), get, put, post, delete: del };
  }

  it('reads the account info', () => {
    const { api, get } = setup();

    api.getInfo();

    expect(get).toHaveBeenCalledWith('/api/account');
  });

  it('sends the current and new password to the password endpoint', () => {
    const { api, put } = setup();

    api.changePassword({ currentPassword: 'old-secret', newPassword: 'Str0ng-passw0rd' });

    expect(put).toHaveBeenCalledWith('/api/account/password', {
      currentPassword: 'old-secret',
      newPassword: 'Str0ng-passw0rd',
    });
  });

  it('sends the current password and new username to the username endpoint', () => {
    const { api, put } = setup();

    api.changeUsername({ currentPassword: 'old-secret', newUsername: 'renamed' });

    expect(put).toHaveBeenCalledWith('/api/account/username', {
      currentPassword: 'old-secret',
      newUsername: 'renamed',
    });
  });

  it('sends both factors when regenerating 2FA', () => {
    const { api, post } = setup();

    api.regenerate2fa({ password: 'old-secret', totpCode: '123456' });

    expect(post).toHaveBeenCalledWith('/api/account/2fa/regenerate', {
      password: 'old-secret',
      totpCode: '123456',
    });
  });

  it('wraps the bare arguments of the 2FA endpoints in a request body', () => {
    const { api, post } = setup();

    api.enable2fa('old-secret');
    api.verifyEnable2fa('123456');
    api.disable2fa('old-secret', '654321');

    expect(post.mock.calls).toEqual([
      ['/api/account/2fa/enable', { password: 'old-secret' }],
      ['/api/account/2fa/enable/verify', { code: '123456' }],
      ['/api/account/2fa/disable', { password: 'old-secret', totpCode: '654321' }],
    ]);
  });

  it('reads and regenerates the api key', () => {
    const { api, get, post } = setup();

    api.getApiKey();
    api.regenerateApiKey();

    expect(get).toHaveBeenCalledWith('/api/account/api-key');
    expect(post).toHaveBeenCalledWith('/api/account/api-key/regenerate', {});
  });

  it('covers the Plex link lifecycle', () => {
    const { api, post, delete: del } = setup();

    api.linkPlex();
    api.verifyPlexLink(42);
    api.unlinkPlex();

    expect(post.mock.calls).toEqual([
      ['/api/account/plex/link', {}],
      ['/api/account/plex/link/verify', { pinId: 42 }],
    ]);
    expect(del).toHaveBeenCalledWith('/api/account/plex/link');
  });

  it('covers the OIDC config and link endpoints', () => {
    const { api, get, put, delete: del } = setup();

    api.getOidcConfig();
    api.updateOidcConfig({ enabled: true });
    api.unlinkOidc();

    expect(get).toHaveBeenCalledWith('/api/account/oidc');
    expect(put).toHaveBeenCalledWith('/api/account/oidc', { enabled: true });
    expect(del).toHaveBeenCalledWith('/api/account/oidc/link');
  });
});
