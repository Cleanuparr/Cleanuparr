import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { ApiError } from '@core/interceptors/error.interceptor';
import { DownloadClientApi } from '@core/api/download-client.api';
import { ConfirmService } from '@core/services/confirm.service';
import { ToastService } from '@core/services/toast.service';
import { ClientConfig, DownloadClientConfig, DownloadClientTypesResponse } from '@shared/models/download-client-config.model';
import { DownloadClientAuthField, DownloadClientCapability, DownloadClientType, DownloadClientTypeName, HttpAuthType } from '@shared/models/enums';
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
  httpAuthType: HttpAuthType.Basic,
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
  httpAuthType: HttpAuthType.Basic,
  urlBase: '',
  downloadDirectorySource: null,
  downloadDirectoryTarget: null,
};

const CONFIG: DownloadClientConfig = { clients: [QBIT, DELUGE] };

const TYPES: DownloadClientTypesResponse = {
  types: [
    {
      typeName: DownloadClientTypeName.qBittorrent,
      authFields: [
        DownloadClientAuthField.Username, DownloadClientAuthField.Password, DownloadClientAuthField.ApiKey,
        DownloadClientAuthField.HttpAuthUsername, DownloadClientAuthField.HttpAuthPassword,
      ],
      capabilities: [DownloadClientCapability.SeedingCleanup, DownloadClientCapability.TagFiltering],
    },
    {
      typeName: DownloadClientTypeName.Deluge,
      authFields: [
        DownloadClientAuthField.Password,
        DownloadClientAuthField.HttpAuthUsername, DownloadClientAuthField.HttpAuthPassword,
      ],
      capabilities: [DownloadClientCapability.SeedingCleanup],
    },
    {
      typeName: DownloadClientTypeName.Transmission,
      authFields: [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
      capabilities: [DownloadClientCapability.SeedingCleanup, DownloadClientCapability.TagFiltering],
    },
    {
      typeName: DownloadClientTypeName.uTorrent,
      authFields: [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
      capabilities: [DownloadClientCapability.SeedingCleanup],
    },
    {
      typeName: DownloadClientTypeName.rTorrent,
      authFields: [DownloadClientAuthField.Username, DownloadClientAuthField.Password],
      capabilities: [],
    },
    {
      typeName: DownloadClientTypeName.Sabnzbd,
      authFields: [
        DownloadClientAuthField.ApiKey,
        DownloadClientAuthField.HttpAuthUsername, DownloadClientAuthField.HttpAuthPassword,
      ],
      capabilities: [DownloadClientCapability.OrphanClaims],
    },
  ],
};

function createApi(config: DownloadClientConfig = CONFIG, types: DownloadClientTypesResponse = TYPES) {
  return {
    getConfig: vi.fn(() => of(config)),
    create: vi.fn(() => of(QBIT)),
    update: vi.fn(() => of(QBIT)),
    delete: vi.fn(() => of(undefined)),
    test: vi.fn(() => of({ message: 'Connected to qBittorrent 4.6.0' })),
    getTypes: vi.fn(() => of(types)),
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

function clientTypeTrigger(fixture: ComponentFixture<DownloadClientsComponent>): HTMLButtonElement {
  return fixture.nativeElement.querySelector('.select-trigger') as HTMLButtonElement;
}

function selectTriggerByLabel(
  fixture: ComponentFixture<DownloadClientsComponent>,
  label: string,
): HTMLButtonElement {
  const selects = Array.from(fixture.nativeElement.querySelectorAll('app-select'));
  const match = selects.find(
    (el) => (el as HTMLElement).querySelector('.select-label')?.textContent!.trim().startsWith(label),
  ) as HTMLElement;
  return match.querySelector('.select-trigger') as HTMLButtonElement;
}

function chooseClientType(
  fixture: ComponentFixture<DownloadClientsComponent>,
  label: string,
): void {
  clientTypeTrigger(fixture).click();
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
      apiKey: '',
      httpAuthType: HttpAuthType.Basic,
      httpAuthUsername: '',
      httpAuthPassword: '',
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

  it('hides username/password and shows a required API key field for SABnzbd', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    fixture.detectChanges();

    chooseClientType(fixture, 'SABnzbd');

    expect(component.clientModel().typeName).toBe(DownloadClientTypeName.Sabnzbd);
    expect(component.showUsernameField()).toBe(false);
    expect(component.showPasswordField()).toBe(false);
    expect(component.showApiKeyField()).toBe(true);
    expect(fieldLabels(fixture)).not.toContain('Username');
    expect(fieldLabels(fixture)).not.toContain('Password');
    expect(fieldLabels(fixture)).toContain('API Key');

    component.clientForm.apiKey().value.set('sab-key');
    fixture.detectChanges();
    expect(hintFor(fixture, 'API Key')).toBe('API key from SABnzbd > Config > General');
  });

  it('blocks saving a SABnzbd client with an empty API key and shows the error', () => {
    const { fixture, component, api } = setup();

    component.openAddModal();
    component.clientForm.name().value.set('SAB box');
    component.clientForm.host().value.set('http://localhost:8080');
    fixture.detectChanges();

    chooseClientType(fixture, 'SABnzbd');

    expect(component.hasModalErrors()).toBe(true);
    expect(component.clientForm.apiKey().errors()[0].message).toBe('API key is required');

    component.saveClient();
    expect(api.create).not.toHaveBeenCalled();
  });

  it('does not require an API key for qBittorrent with an empty key', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    component.clientForm.name().value.set('New client');
    component.clientForm.host().value.set('http://localhost:8080');
    fixture.detectChanges();

    expect(component.clientModel().typeName).toBe(DownloadClientTypeName.qBittorrent);
    expect(component.clientForm.apiKey().value()).toBe('');
    expect(component.hasModalErrors()).toBe(false);
  });

  it('shows no auth fields while the types endpoint is still loading', async () => {
    const types$ = new Subject<DownloadClientTypesResponse>();
    const api = createApi();
    api.getTypes.mockReturnValue(types$);
    const { fixture, component } = setup(api);

    component.openAddModal();
    fixture.detectChanges();

    expect(component.showUsernameField()).toBe(false);
    expect(component.showPasswordField()).toBe(false);
    expect(component.showApiKeyField()).toBe(false);
    expect(fieldLabels(fixture)).not.toContain('Username');
    expect(fieldLabels(fixture)).not.toContain('Password');

    types$.next(TYPES);
    types$.complete();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.showUsernameField()).toBe(true);
    expect(component.showPasswordField()).toBe(true);
  });

  it('does not show the API key field for clients that do not use one', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    fixture.detectChanges();

    chooseClientType(fixture, 'Deluge');

    expect(component.clientModel().typeName).toBe(DownloadClientTypeName.Deluge);
    expect(component.showApiKeyField()).toBe(false);
    expect(fieldLabels(fixture)).not.toContain('API Key');
  });

  it('shows the API key field for qBittorrent alongside username and password', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    fixture.detectChanges();

    expect(component.clientModel().typeName).toBe(DownloadClientTypeName.qBittorrent);
    expect(component.showUsernameField()).toBe(true);
    expect(component.showPasswordField()).toBe(true);
    expect(component.showApiKeyField()).toBe(true);
    expect(fieldLabels(fixture)).toContain('API Key');
    expect(hintFor(fixture, 'API Key'))
      .toBe('qBittorrent 5.2+: Options > WebUI > API key. Leave username and password empty.');
  });

  it('blocks saving a qBittorrent client with both an API key and a password', () => {
    const { fixture, component, api } = setup();

    component.openAddModal();
    component.clientForm.name().value.set('qbit box');
    component.clientForm.host().value.set('http://localhost:8080');
    component.clientForm.password().value.set('secret');
    component.clientForm.apiKey().value.set('qbit-key');
    fixture.detectChanges();

    expect(component.hasModalErrors()).toBe(true);
    expect(component.clientForm.apiKey().errors()[0].message)
      .toBe('Use either an API key or a username and password, not both');

    component.saveClient();
    expect(api.create).not.toHaveBeenCalled();
  });

  it('saves a qBittorrent client with only an API key', () => {
    const { fixture, component, api } = setup();

    component.openAddModal();
    component.clientForm.name().value.set('qbit box');
    component.clientForm.host().value.set('http://localhost:8080');
    component.clientForm.apiKey().value.set('qbit-key');
    fixture.detectChanges();

    expect(component.hasModalErrors()).toBe(false);

    component.saveClient();
    fixture.detectChanges();

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({
        typeName: DownloadClientTypeName.qBittorrent,
        apiKey: 'qbit-key',
      }),
    );
  });

  it('blocks saving a qBittorrent client with both an API key and a username', () => {
    const { fixture, component, api } = setup();

    component.openAddModal();
    component.clientForm.name().value.set('qbit box');
    component.clientForm.host().value.set('http://localhost:8080');
    component.clientForm.username().value.set('admin');
    component.clientForm.apiKey().value.set('qbit-key');
    fixture.detectChanges();

    expect(component.hasModalErrors()).toBe(true);
    expect(component.clientForm.apiKey().errors()[0].message)
      .toBe('Use either an API key or a username and password, not both');

    component.saveClient();
    expect(api.create).not.toHaveBeenCalled();
  });

  it('edits a qBittorrent client with a null username and an API key placeholder without showing the exclusive error', () => {
    const qbitApiKeyOnly: ClientConfig = {
      ...QBIT,
      username: null as unknown as string,
      password: undefined,
      apiKey: 'qbit-placeholder-key',
    };
    const { fixture, component, api } = setup(createApi({ clients: [qbitApiKeyOnly] }));

    component.openEditModal(qbitApiKeyOnly);
    fixture.detectChanges();

    expect(component.hasModalErrors()).toBe(false);

    component.clientForm.name().value.set('qbit box renamed');
    fixture.detectChanges();

    component.saveClient();
    fixture.detectChanges();

    expect(api.update).toHaveBeenCalledWith(
      qbitApiKeyOnly.id,
      expect.objectContaining({ name: 'qbit box renamed', apiKey: 'qbit-placeholder-key' }),
    );
  });

  it('saves a SABnzbd client with the Usenet client type', () => {
    const { fixture, component, api } = setup();

    component.openAddModal();
    component.clientForm.name().value.set('SAB box');
    component.clientForm.host().value.set('http://localhost:8080');
    fixture.detectChanges();

    chooseClientType(fixture, 'SABnzbd');
    component.clientForm.apiKey().value.set('sab-key');
    fixture.detectChanges();

    component.saveClient();
    fixture.detectChanges();

    expect(api.create).toHaveBeenCalledWith(
      expect.objectContaining({
        typeName: DownloadClientTypeName.Sabnzbd,
        apiKey: 'sab-key',
      }),
    );
  });

  it('autofills the url base and switches the hints to the HTTP auth reuse wording for rTorrent', () => {
    const { fixture, component } = setup();

    component.openAddModal();
    fixture.detectChanges();

    expect(hintFor(fixture, 'Username')).toBe('Username for authentication');
    expect(hintFor(fixture, 'Password')).toBe('Password for authentication');
    expect(hintFor(fixture, 'URL Base')).toBe('Optional URL base path, leave blank for default');

    chooseClientType(fixture, 'rTorrent');

    expect(component.clientModel().urlBase).toBe('plugins/httprpc/action.php');
    expect(hintFor(fixture, 'Username')).toBe('Username for the client and its reverse proxy');
    expect(hintFor(fixture, 'Password')).toBe('Password for the client and its reverse proxy');
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
      apiKey: '',
      httpAuthType: HttpAuthType.Basic,
      httpAuthUsername: '',
      httpAuthPassword: '',
      urlBase: '',
      externalUrl: '',
      downloadDirectorySource: '',
      downloadDirectoryTarget: '',
    });
    expect(component.showUsernameField()).toBe(false);
    expect(fixture.nativeElement.textContent).toContain('Edit Client');
  });

  it('locks the client type while editing and unlocks it when adding', () => {
    const { fixture, component } = setup();

    component.openEditModal(QBIT);
    fixture.detectChanges();

    expect(component.clientForm.typeName().disabled()).toBe(true);
    expect(clientTypeTrigger(fixture).disabled).toBe(true);

    component.openAddModal();
    fixture.detectChanges();

    expect(component.clientForm.typeName().disabled()).toBe(false);
    expect(clientTypeTrigger(fixture).disabled).toBe(false);
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
      typeName: DownloadClientTypeName.Transmission,
      host: 'http://localhost:9091',
      username: '',
      password: '',
      apiKey: '',
      httpAuthType: HttpAuthType.Basic,
      httpAuthUsername: '',
      httpAuthPassword: '',
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
      name: 'Renamed',
      typeName: DownloadClientTypeName.qBittorrent,
      host: 'http://localhost:8080',
      username: 'admin',
      password: undefined,
      apiKey: undefined,
      httpAuthType: HttpAuthType.Basic,
      httpAuthUsername: undefined,
      httpAuthPassword: undefined,
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
    api.update.mockReturnValue(throwError(() => ({ message: 'Invalid host address' })));
    const { fixture, component, toast } = setup(api);

    component.openEditModal(QBIT);
    fixture.detectChanges();

    component.saveClient();
    fixture.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Invalid host address');
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
      host: 'http://localhost:8080',
      username: 'admin',
      password: 'secret',
      apiKey: '',
      httpAuthType: HttpAuthType.Basic,
      httpAuthUsername: '',
      httpAuthPassword: '',
      urlBase: '',
      clientId: 'client-qb',
    });
    expect(toast.success).toHaveBeenCalledWith('Connected to qBittorrent 4.6.0');
    expect(component.testing()).toBe(false);
  });

  it('reports a failed connection test and stops the testing spinner', () => {
    const api = createApi();
    api.test.mockReturnValue(throwError(() => ({ message: 'Connection refused' })));
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
    expect(toast.error).toHaveBeenCalledWith('Connection refused');
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

  it('reports pending changes only while the modal is open and edited', () => {
    const { fixture, component } = setup();

    expect(component.hasPendingChanges()).toBe(false);

    component.openAddModal();
    fixture.detectChanges();
    expect(component.hasPendingChanges()).toBe(false);

    component.clientForm.name().value.set('New client');
    fixture.detectChanges();
    expect(component.hasPendingChanges()).toBe(true);

    component.clientForm.host().value.set('http://localhost:9091');
    component.saveClient();
    fixture.detectChanges();

    expect(component.modalVisible()).toBe(false);
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('shows the connection error state when loading fails and recovers on retry', () => {
    const api = createApi();
    api.getConfig.mockReturnValue(throwError(() => new Error('offline')));
    const { fixture, component, toast } = setup(api);

    expect(component.loadError()).toBe(true);
    expect(component.clients()).toEqual([]);
    expect(toast.error).toHaveBeenCalledWith('Failed to load download clients: offline');
    expect(fixture.nativeElement.textContent).toContain('Could not connect to server');

    api.getConfig.mockReturnValue(of(CONFIG));
    component.retry();
    fixture.detectChanges();

    expect(component.loadError()).toBe(false);
    expect(text(fixture, '.item-row__name')).toEqual(['qBit box', 'Deluge box']);
  });

  it('shows the connection error state when the types endpoint fails and recovers on retry', () => {
    const api = createApi();
    api.getTypes.mockReturnValue(throwError(() => new Error('offline')));
    const { fixture, component, toast } = setup(api);

    expect(component.loadError()).toBe(true);
    expect(component.clients()).toEqual([]);
    expect(toast.error).toHaveBeenCalledWith('Failed to load download clients: offline');
    expect(fixture.nativeElement.textContent).toContain('Could not connect to server');

    api.getTypes.mockReturnValue(of(TYPES));
    component.retry();
    fixture.detectChanges();

    expect(component.loadError()).toBe(false);
    expect(text(fixture, '.item-row__name')).toEqual(['qBit box', 'Deluge box']);
  });

  it('reports a failed create and keeps the modal open', () => {
    const api = createApi();
    api.create.mockReturnValue(throwError(() => new ApiError('Invalid port')));
    const { fixture, component, toast } = setup(api);

    component.openAddModal();
    component.clientForm.name().value.set('New client');
    component.clientForm.host().value.set('http://localhost:9091');
    fixture.detectChanges();

    component.saveClient();
    fixture.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Invalid port');
    expect(component.modalVisible()).toBe(true);
    expect(component.saving()).toBe(false);
  });

  it('reports a failed delete and keeps the client list open', async () => {
    const api = createApi();
    api.delete.mockReturnValue(throwError(() => new ApiError('Client in use')));
    const { fixture, component, toast, confirm } = setup(api);

    const accepted = component.deleteClient(QBIT);
    confirm.accept();
    await accepted;
    fixture.detectChanges();

    expect(toast.error).toHaveBeenCalledWith('Client in use');
    expect(component.clients()).toEqual([QBIT, DELUGE]);
  });

  describe('HTTP authentication', () => {
    const typeLabels = ['qBittorrent', 'Deluge', 'Transmission', 'uTorrent', 'rTorrent', 'SABnzbd'];
    const dedicatedFieldLabels = ['qBittorrent', 'Deluge', 'SABnzbd'];

    it('shows the HTTP authentication type selector for every client type', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      fixture.detectChanges();

      for (const label of typeLabels) {
        chooseClientType(fixture, label);
        expect(selectTriggerByLabel(fixture, 'HTTP Authentication')).toBeTruthy();
      }
    });

    it('shows HTTP auth username and password fields only for qBittorrent, Deluge and SABnzbd', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      fixture.detectChanges();

      for (const label of typeLabels) {
        chooseClientType(fixture, label);
        const expected = dedicatedFieldLabels.includes(label);
        expect(component.showHttpAuthCredentialFields()).toBe(expected);
        if (expected) {
          expect(fieldLabels(fixture)).toContain('HTTP Auth Username');
          expect(fieldLabels(fixture)).toContain('HTTP Auth Password');
        } else {
          expect(fieldLabels(fixture)).not.toContain('HTTP Auth Username');
          expect(fieldLabels(fixture)).not.toContain('HTTP Auth Password');
        }
      }
    });

    it('sends the HTTP auth type and dedicated credentials in the create payload', () => {
      const { fixture, component, api } = setup();

      component.openAddModal();
      component.clientForm.name().value.set('qbit box');
      component.clientForm.host().value.set('http://localhost:8080');
      component.clientForm.httpAuthType().value.set(HttpAuthType.Digest);
      component.clientForm.httpAuthUsername().value.set('proxy-user');
      component.clientForm.httpAuthPassword().value.set('proxy-pass');
      fixture.detectChanges();

      component.saveClient();
      fixture.detectChanges();

      expect(api.create).toHaveBeenCalledWith(
        expect.objectContaining({
          httpAuthType: HttpAuthType.Digest,
          httpAuthUsername: 'proxy-user',
          httpAuthPassword: 'proxy-pass',
        }),
      );
    });

    it('sends the reused username and password as HTTP auth credentials for rTorrent', () => {
      const { fixture, component, api } = setup();

      component.openAddModal();
      component.clientForm.name().value.set('rt box');
      component.clientForm.host().value.set('http://localhost:8080');
      fixture.detectChanges();

      chooseClientType(fixture, 'rTorrent');
      component.clientForm.httpAuthType().value.set(HttpAuthType.Digest);
      component.clientForm.username().value.set('rt-user');
      component.clientForm.password().value.set('rt-pass');
      fixture.detectChanges();

      component.saveClient();
      fixture.detectChanges();

      expect(api.create).toHaveBeenCalledWith(
        expect.objectContaining({
          typeName: DownloadClientTypeName.rTorrent,
          httpAuthType: HttpAuthType.Digest,
          username: 'rt-user',
          password: 'rt-pass',
        }),
      );
    });

    it('defaults the HTTP auth type to Basic for a client with none stored', () => {
      const clientWithoutAuthType: ClientConfig = { ...QBIT, httpAuthType: undefined as unknown as HttpAuthType };
      const { fixture, component } = setup(createApi({ clients: [clientWithoutAuthType] }));

      component.openEditModal(clientWithoutAuthType);
      fixture.detectChanges();

      expect(component.clientModel().httpAuthType).toBe(HttpAuthType.Basic);
    });

    it('does not require a Digest username while the types are still loading', () => {
      const types$ = new Subject<DownloadClientTypesResponse>();
      const api = createApi();
      api.getTypes.mockReturnValue(types$);
      const { fixture, component } = setup(api);

      component.openAddModal();
      component.clientForm.name().value.set('qbit box');
      component.clientForm.host().value.set('http://localhost:8080');
      component.clientForm.httpAuthType().value.set(HttpAuthType.Digest);
      fixture.detectChanges();

      expect(component.hasModalErrors()).toBe(false);
      expect(component.clientForm.httpAuthUsername().errors()).toEqual([]);
      expect(component.clientForm.username().errors()).toEqual([]);
    });

    it('loads the saved HTTP auth type and credentials into the form', () => {
      const qbitWithHttpAuth: ClientConfig = {
        ...QBIT,
        httpAuthType: HttpAuthType.Digest,
        httpAuthUsername: 'proxy-user',
        httpAuthPassword: 'proxy-pass',
      };
      const { fixture, component } = setup(createApi({ clients: [qbitWithHttpAuth] }));

      component.openEditModal(qbitWithHttpAuth);
      fixture.detectChanges();

      expect(component.clientModel().httpAuthType).toBe(HttpAuthType.Digest);
      expect(component.clientModel().httpAuthUsername).toBe('proxy-user');
      expect(component.clientModel().httpAuthPassword).toBe('proxy-pass');
    });

    it('marks the form dirty when only the HTTP auth username changes', () => {
      const { fixture, component } = setup();

      component.openEditModal(QBIT);
      fixture.detectChanges();
      expect(component.hasPendingChanges()).toBe(false);

      component.clientForm.httpAuthUsername().value.set('proxy-user');
      fixture.detectChanges();

      expect(component.hasPendingChanges()).toBe(true);
    });

    it('requires a username when Digest is selected for a client with dedicated HTTP auth fields', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      component.clientForm.name().value.set('qbit box');
      component.clientForm.host().value.set('http://localhost:8080');
      component.clientForm.httpAuthType().value.set(HttpAuthType.Digest);
      fixture.detectChanges();

      expect(component.hasModalErrors()).toBe(true);
      expect(component.clientForm.httpAuthUsername().errors()[0].message)
        .toBe('HTTP Digest authentication requires a username');
    });

    it('requires a username when Digest is selected for a client reusing username/password', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      component.clientForm.name().value.set('rt box');
      component.clientForm.host().value.set('http://localhost:8080');
      fixture.detectChanges();

      chooseClientType(fixture, 'rTorrent');
      component.clientForm.httpAuthType().value.set(HttpAuthType.Digest);
      fixture.detectChanges();

      expect(component.hasModalErrors()).toBe(true);
      expect(component.clientForm.username().errors()[0].message)
        .toBe('HTTP Digest authentication requires a username');
    });

    it('blocks an API key combined with HTTP auth credentials for qBittorrent', () => {
      const { fixture, component, api } = setup();

      component.openAddModal();
      component.clientForm.name().value.set('qbit box');
      component.clientForm.host().value.set('http://localhost:8080');
      component.clientForm.apiKey().value.set('qbit-key');
      component.clientForm.httpAuthUsername().value.set('proxy-user');
      fixture.detectChanges();

      expect(component.hasModalErrors()).toBe(true);
      expect(component.clientForm.apiKey().errors()[0].message)
        .toBe("API key can't be combined with HTTP authentication. Use qBittorrent username/password instead.");

      component.saveClient();
      expect(api.create).not.toHaveBeenCalled();
    });

    it('accepts an API key combined with HTTP auth credentials for SABnzbd', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      component.clientForm.name().value.set('sab box');
      component.clientForm.host().value.set('http://localhost:8080');
      fixture.detectChanges();

      chooseClientType(fixture, 'SABnzbd');
      component.clientForm.apiKey().value.set('sab-key');
      component.clientForm.httpAuthUsername().value.set('proxy-user');
      fixture.detectChanges();

      expect(component.clientForm.apiKey().errors()).toEqual([]);
      expect(component.hasModalErrors()).toBe(false);
    });

    it('clears HTTP auth credentials when switching from a client with them to one without', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      fixture.detectChanges();

      chooseClientType(fixture, 'qBittorrent');
      component.clientForm.httpAuthUsername().value.set('proxy-user');
      component.clientForm.httpAuthPassword().value.set('proxy-pass');
      fixture.detectChanges();

      expect(component.clientModel().httpAuthUsername).toBe('proxy-user');
      expect(component.clientModel().httpAuthPassword).toBe('proxy-pass');

      chooseClientType(fixture, 'Transmission');

      expect(component.clientModel().httpAuthUsername).toBe('');
      expect(component.clientModel().httpAuthPassword).toBe('');
    });

    it('shows the HTTP auth type selector for types without dedicated fields and uses shared username/password', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      fixture.detectChanges();

      chooseClientType(fixture, 'rTorrent');

      expect(selectTriggerByLabel(fixture, 'HTTP Authentication')).toBeTruthy();
      expect(component.showHttpAuthCredentialFields()).toBe(false);
      expect(fieldLabels(fixture)).toContain('Username');
      expect(fieldLabels(fixture)).toContain('Password');
    });

    it('shows different hints for clients with dedicated HTTP auth fields vs reused fields', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      fixture.detectChanges();

      chooseClientType(fixture, 'qBittorrent');

      expect(hintFor(fixture, 'Username')).toBe('Username for authentication');
      expect(hintFor(fixture, 'Password')).toBe('Password for authentication');
      expect(component.httpAuthHint()).toBe('For a reverse proxy that asks for HTTP auth. Pick Digest only if the proxy challenges with Digest.');

      chooseClientType(fixture, 'rTorrent');

      expect(hintFor(fixture, 'Username')).toBe('Username for the client and its reverse proxy');
      expect(hintFor(fixture, 'Password')).toBe('Password for the client and its reverse proxy');
      expect(component.httpAuthHint()).toBe('For a reverse proxy that asks for HTTP auth. Uses the username and password above. Pick Digest only if the proxy challenges with Digest.');
    });

    it('accepts Digest without error when a username is provided for a client with dedicated fields', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      component.clientForm.name().value.set('qbit box');
      component.clientForm.host().value.set('http://localhost:8080');
      component.clientForm.httpAuthType().value.set(HttpAuthType.Digest);
      component.clientForm.httpAuthUsername().value.set('proxy-user');
      fixture.detectChanges();

      expect(component.clientForm.httpAuthUsername().errors()).toEqual([]);
      expect(component.hasModalErrors()).toBe(false);
    });

    it('accepts Digest without error when a username is provided for a client reusing fields', () => {
      const { fixture, component } = setup();

      component.openAddModal();
      component.clientForm.name().value.set('rt box');
      component.clientForm.host().value.set('http://localhost:8080');
      fixture.detectChanges();

      chooseClientType(fixture, 'rTorrent');
      component.clientForm.httpAuthType().value.set(HttpAuthType.Digest);
      component.clientForm.username().value.set('rt-user');
      fixture.detectChanges();

      expect(component.clientForm.username().errors()).toEqual([]);
      expect(component.hasModalErrors()).toBe(false);
    });

    it('loads HTTP auth fields when editing an existing client', () => {
      const delugeWithBasic: ClientConfig = {
        ...DELUGE,
        httpAuthType: HttpAuthType.Basic,
        httpAuthUsername: 'proxy-user',
        httpAuthPassword: 'proxy-pass',
      };
      const { fixture, component } = setup(createApi({ clients: [delugeWithBasic] }));

      component.openEditModal(delugeWithBasic);
      fixture.detectChanges();

      expect(component.clientModel().httpAuthType).toBe(HttpAuthType.Basic);
      expect(component.clientModel().httpAuthUsername).toBe('proxy-user');
      expect(component.clientModel().httpAuthPassword).toBe('proxy-pass');
    });

    it('sends updated HTTP auth fields when editing a client', () => {
      const qbitWithHttpAuth: ClientConfig = {
        ...QBIT,
        httpAuthType: HttpAuthType.Basic,
        httpAuthUsername: 'proxy-user',
        httpAuthPassword: 'proxy-pass',
      };
      const { fixture, component, api } = setup(createApi({ clients: [qbitWithHttpAuth] }));

      component.openEditModal(qbitWithHttpAuth);
      component.clientForm.httpAuthType().value.set(HttpAuthType.Digest);
      component.clientForm.httpAuthUsername().value.set('updated-user');
      fixture.detectChanges();

      component.saveClient();
      fixture.detectChanges();

      expect(api.update).toHaveBeenCalledWith(
        'client-qb',
        expect.objectContaining({
          httpAuthType: HttpAuthType.Digest,
          httpAuthUsername: 'updated-user',
          httpAuthPassword: 'proxy-pass',
        }),
      );
    });

    it('drops empty HTTP auth fields when updating a client to Basic', () => {
      const qbitWithHttpAuth: ClientConfig = {
        ...QBIT,
        httpAuthType: HttpAuthType.Digest,
        httpAuthUsername: 'proxy-user',
        httpAuthPassword: 'proxy-pass',
      };
      const { fixture, component, api } = setup(createApi({ clients: [qbitWithHttpAuth] }));

      component.openEditModal(qbitWithHttpAuth);
      component.clientForm.httpAuthType().value.set(HttpAuthType.Basic);
      component.clientForm.httpAuthUsername().value.set('');
      component.clientForm.httpAuthPassword().value.set('');
      fixture.detectChanges();

      component.saveClient();
      fixture.detectChanges();

      expect(api.update).toHaveBeenCalledWith(
        'client-qb',
        expect.objectContaining({
          httpAuthType: HttpAuthType.Basic,
          httpAuthUsername: undefined,
          httpAuthPassword: undefined,
        }),
      );
    });
  });
});
