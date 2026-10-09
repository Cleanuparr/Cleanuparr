import { HttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { DownloadClientApi } from './download-client.api';

describe('DownloadClientApi', () => {
  function setup() {
    const get = vi.fn(() => of(null));
    TestBed.configureTestingModule({
      providers: [{ provide: HttpClient, useValue: { get } }],
    });
    return { api: TestBed.inject(DownloadClientApi), get };
  }

  it('fetches the download client types from the types endpoint', () => {
    const { api, get } = setup();

    api.getTypes();

    expect(get).toHaveBeenCalledWith('/api/configuration/download_client/types');
  });
});
