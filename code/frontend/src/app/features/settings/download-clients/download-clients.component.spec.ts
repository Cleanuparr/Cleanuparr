import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DownloadClientApi } from '@core/api/download-client.api';
import { ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { ClientConfig, DownloadClientConfig } from '@shared/models/download-client-config.model';
import { DownloadClientType, DownloadClientTypeName } from '@shared/models/enums';
import { DownloadClientsComponent } from './download-clients.component';

const QBIT: ClientConfig = {
  enabled: true,
  id: 'client-qb',
  name: 'qBit box',
  type: DownloadClientType.Torrent,
  typeName: DownloadClientTypeName.qBittorrent,
  host: 'http://localhost:8080',
  username: 'admin',
  password: 'secret',
  urlBase: '',
  externalUrl: 'https://qbit.example.com',
  downloadDirectorySource: '/downloads',
  downloadDirectoryTarget: '/mnt/data/downloads',
};

const DELUGE: ClientConfig = {
  enabled: false,
  id: 'client-dl',
  name: 'Deluge box',
  type: DownloadClientType.Torrent,
  typeName: DownloadClientTypeName.Deluge,
  host: 'http://localhost:8112',
  username: 'legacy',
  urlBase: '',
  downloadDirectorySource: null,
  downloadDirectoryTarget: null,
};

const CONFIG: DownloadClientConfig = { clients: [QBIT, DELUGE] };

function createApi(config: DownloadClientConfig = CONFIG) {
  return {
    getConfig: vi.fn(() => of(config)),
    create: vi.fn(() => of(QBIT)),
    update: vi.fn(() => of(QBIT)),
    delete: vi.fn(() => of(undefined)),
    test: vi.fn(() => of({ message: 'Connected to qBittorrent 4.6.0' })),
  };
}

function createToast() {
  return { success: vi.fn(), error: vi.fn() };
}

interface Setup {
  fixture: ComponentFixture<DownloadClientsComponent>;
  component: DownloadClientsComponent;
  api: ReturnType<typeof createApi>;
  toast: ReturnType<typeof createToast>;
  confirm: ConfirmService;
}

function text(fixture: ComponentFixture<DownloadClientsComponent>, selector: string): string[] {
  return Array.from(fixture.nativeElement.querySelectorAll(selector)).map((el) =>
    (el as HTMLElement).textContent!.trim(),
  );
}

function fieldLabels(fixture: ComponentFixture<DownloadClientsComponent>): string[] {
  return Array.from(fixture.nativeElement.querySelectorAll('.modal-form .input-label')).map((el) =>
    (el as HTMLElement).textContent!.trim(),
  );
}

function hintFor(fixture: ComponentFixture<DownloadClientsComponent>, label: string): string {
  const fields = Array.from(fixture.nativeElement.querySelectorAll('.modal-form app-input'));
  const field = fields.find(
    (el) => (el as HTMLElement).querySelector('.input-label')?.textContent!.trim() === label,
  ) as HTMLElement | undefined;
  return field?.querySelector('.input-hint')?.textContent!.trim() ?? '';
}

function chooseClientType(
  fixture: ComponentFixture<DownloadClientsComponent>,
  label: string,
): void {
  const trigger = fixture.nativeElement.querySelector('.select-trigger') as HTMLButtonElement;
  trigger.click();
  fixture.detectChanges();

  const option = Array.from(fixture.nativeElement.querySelectorAll('.select-option')).find(
    (el) => (el as HTMLElement).textContent!.trim() === label,
  ) as HTMLButtonElement;
  option.click();
  fixture.detectChanges();
}

describe('DownloadClientsComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setup(api = createApi(), toast = createToast()): Setup {
    TestBed.configureTestingModule({
      providers: [
        { provide: DownloadClientApi, useValue: api },
        { provide: ToastService, useValue: toast },
      ],
    });

    const fixture = TestBed.createComponent(DownloadClientsComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      api,
      toast,
      confirm: TestBed.inject(ConfirmService),
    };
  }

  it('loads the configured clients and renders their type and host', () => {
    const { fixture, component } = setup();

    expect(component.clients()).toEqual([QBIT, DELUGE]);
    expect(text(fixture, '.item-row__name')).toEqual(['qBit box', 'Deluge box']);
    expect(text(fixture, '.item-row__detail')).toEqual([
      'http://localhost:8080',
      'http://localhost:8112',
    ]);
    expect(text(fixture, '.item-row app-badge')).toEqual([
      'Enabled',
      'qBittorrent',
      'Disabled',
      'Deluge',
    ]);
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('shows an empty state when no clients are configured', () => {
    const { fixture } = setup(createApi({ clients: [] }));

    expect(fixture.nativeElement.textContent).toContain('No download clients');
  });

  it('opens a blank add modal and blocks saving until name and host are filled in', () => {
    const { fixture, component, api } = setup();

    component.openAddModal();
    fixture.detectChanges();

    expect(component.editingClient()).toBeNull();
    expect(component.clientModel()).toEqual({
      enabled: true,
      name: '',
      typeName: DownloadClientTypeName.qBittorrent,
      host: '',
      username: '',
      password: '',
      urlBase: '',
      externalUrl: '',
      downloadDirectorySource: '',
      downloadDirectoryTarget: '',
    });
    expect(fixture.nativeElement.textContent).toContain('Add Client');
    expect(component.hasModalErrors()).toBe(true);
    expect(component.clientForm.name().errors()[0].message).toBe('Name is required');
    expect(component.clientForm.host().errors()[0].message).toBe('Host is required');

    component.saveClient();
    expect(api.create).not.toHaveBeenCalled();
    expect(component.saving()).toBe(false);

    component.clientForm.name().value.set('New client');
    component.clientForm.host().value.set('http://localhost:8080');
    fixture.detectChanges();

    expect(component.hasModalErrors()).toBe(false);
  });

  it('hides the username field and clears the username when switching to Deluge', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    component.clientForm.username().value.set('admin');
    fixture.detectChanges();

    expect(component.showUsernameField()).toBe(true);
    expect(fieldLabels(fixture)).toContain('Username');

    chooseClientType(fixture, 'Deluge');

    expect(component.clientModel().typeName).toBe(DownloadClientTypeName.Deluge);
    expect(component.clientModel().username).toBe('');
    expect(component.showUsernameField()).toBe(false);
    expect(fieldLabels(fixture)).not.toContain('Username');
    expect(fieldLabels(fixture)).toContain('Password');
  });

  it('autofills the url base and switches the hints to HTTP Basic Auth for rTorrent', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    fixture.detectChanges();

    expect(hintFor(fixture, 'Username')).toBe('Username for authentication');
    expect(hintFor(fixture, 'Password')).toBe('Password for authentication');
    expect(hintFor(fixture, 'URL Base')).toBe('Optional URL base path, leave blank for default');

    chooseClientType(fixture, 'rTorrent');

    expect(component.clientModel().urlBase).toBe('plugins/httprpc/action.php');
    expect(hintFor(fixture, 'Username')).toBe('Username for HTTP Basic Auth');
    expect(hintFor(fixture, 'Password')).toBe('Password for HTTP Basic Auth');
    expect(hintFor(fixture, 'URL Base')).toContain('XMLRPC endpoint');
  });

  it('replaces a previously autofilled url base but never a user supplied one', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    fixture.detectChanges();

    chooseClientType(fixture, 'Transmission');
    expect(component.clientModel().urlBase).toBe('transmission');

    chooseClientType(fixture, 'rTorrent');
    expect(component.clientModel().urlBase).toBe('plugins/httprpc/action.php');

    component.clientForm.urlBase().value.set('/custom/base');
    fixture.detectChanges();

    chooseClientType(fixture, 'Transmission');
    expect(component.clientModel().urlBase).toBe('/custom/base');

    chooseClientType(fixture, 'qBittorrent');
    expect(component.clientModel().urlBase).toBe('/custom/base');
  });

  it('clears an autofilled url base when switching to a type that has no default', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    fixture.detectChanges();

    chooseClientType(fixture, 'Transmission');
    expect(component.clientModel().urlBase).toBe('transmission');

    chooseClientType(fixture, 'qBittorrent');
    expect(component.clientModel().urlBase).toBe('');
  });

  it('loads an existing client into the modal without clobbering its type specific values', () => {
    const { fixture, component } = setup();

    component.openEditModal(DELUGE);
    fixture.detectChanges();

    expect(component.editingClient()).toBe(DELUGE);
    expect(component.clientModel()).toEqual({
      enabled: false,
      name: 'Deluge box',
      typeName: DownloadClientTypeName.Deluge,
      host: 'http://localhost:8112',
      username: 'legacy',
      password: '',
      urlBase: '',
      externalUrl: '',
      downloadDirectorySource: '',
      downloadDirectoryTarget: '',
    });
    expect(component.showUsernameField()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Edit Client');
  });

  it('creates a client from the modal values and reloads the list', () => {
    const { fixture, component, api, toast } = setup();

    component.openAddModal();
    component.clientForm.name().value.set('New client');
    component.clientForm.host().value.set('http://localhost:9091');
    fixture.detectChanges();

    chooseClientType(fixture, 'Transmission');

    component.saveClient();
    fixture.detectChanges();

    expect(api.create).toHaveBeenCalledWith({
      enabled: true,
      name: 'New client',
      type: DownloadClientType.Torrent,
      typeName: DownloadClientTypeName.Transmission,
      host: 'http://localhost:9091',
      username: '',
      password: '',
      urlBase: 'transmission',
      externalUrl: undefined,
      downloadDirectorySource: null,
      downloadDirectoryTarget: null,
    });
    expect(toast.success).toHaveBeenCalledWith('Client added');
    expect(component.modalVisible()).toBe(false);
    expect(api.getConfig).toHaveBeenCalledTimes(2);
  });

  it('updates an existing client by id and drops the blank optional values', () => {
    const { fixture, component, api, toast } = setup();

    component.openEditModal(QBIT);
    component.clientForm.name().value.set('Renamed');
    component.clientForm.password().value.set('');
    component.clientForm.externalUrl().value.set('');
    component.clientForm.downloadDirectorySource().value.set('');
    component.clientForm.downloadDirectoryTarget().value.set('');
    fixture.detectChanges();

    component.saveClient();
    fixture.detectChanges();

    expect(api.update).toHaveBeenCalledWith('client-qb', {
      enabled: true,
      id: 'client-qb',
      name: 'Renamed',
      type: DownloadClientType.Torrent,
      typeName: DownloadClientTypeName.qBittorrent,
      host: 'http://localhost:8080',
      username: 'admin',
      password: undefined,
      urlBase: '',
      externalUrl: undefined,
      downloadDirectorySource: null,
      downloadDirectoryTarget: null,
    });
    expect(api.create).not.toHaveBeenCalled();
    expect(toast.success).toHaveBeenCalledWith('Client updated');
  });

  it('keeps the modal open and reports the failure when the update fails', () => {
    const api = createApi();
    api.update.mockReturnValue(throwError(() => new Error('boom')));
    const { fixture, component, toast } = setup(api);

    component.openEditModal(QBIT);
    fixture.detectChanges();

    component.saveClient();
    fixture.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Failed to update client');
    expect(component.modalVisible()).toBe(true);
    expect(component.saving()).toBe(false);
  });

  it('tests the connection for the edited client and surfaces the server message', () => {
    const { fixture, component, api, toast } = setup();

    component.openEditModal(QBIT);
    fixture.detectChanges();

    component.testConnection();
    fixture.detectChanges();

    expect(api.test).toHaveBeenCalledWith({
      typeName: DownloadClientTypeName.qBittorrent,
      type: DownloadClientType.Torrent,
      host: 'http://localhost:8080',
      username: 'admin',
      password: 'secret',
      urlBase: '',
      clientId: 'client-qb',
    });
    expect(toast.success).toHaveBeenCalledWith('Connected to qBittorrent 4.6.0');
    expect(component.testing()).toBe(false);
  });

  it('reports a failed connection test and stops the testing spinner', () => {
    const api = createApi();
    api.test.mockReturnValue(throwError(() => new Error('refused')));
    const { fixture, component, toast } = setup(api);

    component.openAddModal();
    fixture.detectChanges();

    component.testConnection();
    fixture.detectChanges();

    expect(api.test).toHaveBeenCalledWith(
      expect.objectContaining({
        typeName: DownloadClientTypeName.qBittorrent,
        clientId: undefined,
      }),
    );
    expect(toast.error).toHaveBeenCalledWith('Connection test failed');
    expect(component.testing()).toBe(false);
  });

  it('deletes a client only after the destructive confirmation is accepted', async () => {
    const { fixture, component, api, toast, confirm } = setup();

    const cancelled = component.deleteClient(QBIT);
    expect(confirm.state()).toMatchObject({
      title: 'Delete Client',
      confirmLabel: 'Delete',
      destructive: true,
    });
    expect(confirm.state()!.message).toContain('qBit box');
    confirm.cancel();
    await cancelled;

    expect(api.delete).not.toHaveBeenCalled();

    const accepted = component.deleteClient(QBIT);
    confirm.accept();
    await accepted;
    fixture.detectChanges();

    expect(api.delete).toHaveBeenCalledWith('client-qb');
    expect(toast.success).toHaveBeenCalledWith('Client deleted');
    expect(api.getConfig).toHaveBeenCalledTimes(2);
  });

  it('shows the connection error state when loading fails and recovers on retry', () => {
    const api = createApi();
    api.getConfig.mockReturnValue(throwError(() => new Error('offline')));
    const { fixture, component, toast } = setup(api);

    expect(component.loadError()).toBe(true);
    expect(component.clients()).toEqual([]);
    expect(toast.error).toHaveBeenCalledWith('Failed to load download clients');
    expect(fixture.nativeElement.textContent).toContain('Could not connect to server');

    api.getConfig.mockReturnValue(of(CONFIG));
    component.retry();
    fixture.detectChanges();

    expect(component.loadError()).toBe(false);
    expect(text(fixture, '.item-row__name')).toEqual(['qBit box', 'Deluge box']);
  });
});
