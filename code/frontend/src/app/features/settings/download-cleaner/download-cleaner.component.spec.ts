import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { CdkDragDrop } from '@angular/cdk/drag-drop';
import { of } from 'rxjs';
import { DownloadCleanerApi } from '@core/api/download-cleaner.api';
import { ConfirmService } from '@core/services/confirm.service';
import {
  DownloadCleanerConfig,
  OrphanedFilesConfig,
  SeedingRule,
} from '@shared/models/download-cleaner-config.model';
import { DownloadClientTypeName, ScheduleUnit, TorrentPrivacyType } from '@shared/models/enums';
import { DownloadCleanerComponent } from './download-cleaner.component';

const RULE_A: SeedingRule = {
  id: 'rule-a',
  name: 'Movies cleanup',
  categories: ['movies'],
  trackerPatterns: [],
  priority: 0,
  privacyType: TorrentPrivacyType.Public,
  maxRatio: 2,
  minSeedTime: 0,
  maxSeedTime: 240,
  deleteSourceFiles: true,
};

const RULE_B: SeedingRule = {
  id: 'rule-b',
  name: 'TV cleanup',
  categories: ['tv'],
  trackerPatterns: ['tracker.example'],
  priority: 1,
  privacyType: TorrentPrivacyType.Private,
  maxRatio: -1,
  minSeedTime: 24,
  maxSeedTime: 720,
  deleteSourceFiles: false,
};

const CONFIG: DownloadCleanerConfig = {
  enabled: true,
  cronExpression: '0 0/15 * ? * * *',
  useAdvancedScheduling: false,
  ignoredDownloads: ['skip-me'],
  clients: [
    {
      downloadClientId: 'client-qb',
      downloadClientName: 'qBit box',
      downloadClientEnabled: true,
      downloadClientTypeName: DownloadClientTypeName.qBittorrent,
      seedingRules: [RULE_A, RULE_B],
      unlinkedConfig: {
        enabled: true,
        targetCategory: 'unlinked',
        useTag: true,
        ignoredRootDirs: ['/mnt/cross-seed'],
        categories: ['tv'],
      },
      deadTorrentConfig: {
        enabled: true,
        targetCategory: 'dead',
        useTag: false,
        maxStrikes: 5,
        categories: ['movies'],
      },
      orphanedFilesConfig: {
        enabled: true,
        scanDirectories: ['/data/torrents'],
        orphanedDirectory: '/data/orphaned',
        excludePatterns: ['*.nfo'],
        minFileAgeHours: 12,
        purgeAfterHours: 48,
      },
    },
    {
      downloadClientId: 'client-rt',
      downloadClientName: 'rTorrent box',
      downloadClientEnabled: true,
      downloadClientTypeName: DownloadClientTypeName.rTorrent,
      seedingRules: [],
      unlinkedConfig: null,
      deadTorrentConfig: null,
      orphanedFilesConfig: null,
    },
  ],
};

function createApi(config: DownloadCleanerConfig, reloadedRules: SeedingRule[]) {
  return {
    getConfig: vi.fn(() => of(config)),
    getSeedingRules: vi.fn(() => of(reloadedRules)),
    deleteSeedingRule: vi.fn(() => of(undefined)),
    reorderSeedingRules: vi.fn(() => of(undefined)),
    updateUnlinkedConfig: vi.fn(() => of(undefined)),
    updateDeadTorrentConfig: vi.fn(() => of(undefined)),
    updateOrphanedFilesConfig: vi.fn((clientId: string, cfg: OrphanedFilesConfig) => of(cfg)),
    updateConfig: vi.fn(() => of(undefined)),
  };
}

interface Setup {
  fixture: ComponentFixture<DownloadCleanerComponent>;
  component: DownloadCleanerComponent;
  api: ReturnType<typeof createApi>;
  confirm: ConfirmService;
}

function dropEvent(previousIndex: number, currentIndex: number): CdkDragDrop<SeedingRule[]> {
  return { previousIndex, currentIndex } as unknown as CdkDragDrop<SeedingRule[]>;
}

describe('DownloadCleanerComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  async function setup(
    config: DownloadCleanerConfig = CONFIG,
    reloadedRules: SeedingRule[] = [RULE_B],
  ): Promise<Setup> {
    const api = createApi(config, reloadedRules);

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), { provide: DownloadCleanerApi, useValue: api }],
    });

    const fixture = TestBed.createComponent(DownloadCleanerComponent);
    fixture.detectChanges();
    await Promise.resolve();
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      api,
      confirm: TestBed.inject(ConfirmService),
    };
  }

  it('stays clean when the stored interval is not one of the offered options', async () => {
    const { component } = await setup({ ...CONFIG, cronExpression: '0 0/7 * ? * * *' });

    expect(component.dcForm.scheduleUnit().value()).toBe(ScheduleUnit.Minutes);
    expect(component.dcForm.scheduleEvery().value()).toBe(1);
    expect(component.dirty()).toBe(false);
  });

  it('loads the global config, parses the cron into schedule fields and selects the first client', async () => {
    const { fixture, component } = await setup();

    expect(component.dcForm.enabled().value()).toBe(true);
    expect(component.dcForm.useAdvancedScheduling().value()).toBe(false);
    expect(component.dcForm.scheduleUnit().value()).toBe(ScheduleUnit.Minutes);
    expect(component.dcForm.scheduleEvery().value()).toBe(15);
    expect(component.dcForm.ignoredDownloads().value()).toEqual(['skip-me']);
    expect(component.selectedClientId()).toBe('client-qb');
    expect(component.clientOptions()).toEqual([
      { label: 'qBit box', value: 'client-qb' },
      { label: 'rTorrent box', value: 'client-rt' },
    ]);
    expect(component.hasPendingChanges()).toBe(false);

    component.seedingRulesExpanded.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Movies cleanup');
    expect(fixture.nativeElement.textContent).toContain('TV cleanup');
  });

  it('hydrates the per-client sections from the selected client and reports them clean', async () => {
    const { component } = await setup();

    expect(component.unlinkedModel()).toEqual({
      enabled: true,
      targetCategory: 'unlinked',
      useTag: true,
      ignoredRootDirs: ['/mnt/cross-seed'],
      categories: ['tv'],
    });
    expect(component.deadTorrentModel()).toEqual({
      enabled: true,
      targetCategory: 'dead',
      useTag: false,
      maxStrikes: 5,
      categories: ['movies'],
    });
    expect(component.orphanedFilesModel()).toEqual({
      enabled: true,
      scanDirectories: ['/data/torrents'],
      orphanedDirectory: '/data/orphaned',
      excludePatterns: ['*.nfo'],
      minFileAgeHours: 12,
      purgeAfterHours: 48,
    });
    expect(component.unlinkedDirty()).toBe(false);
    expect(component.deadTorrentDirty()).toBe(false);
    expect(component.orphanedFilesDirty()).toBe(false);
  });

  it('falls back to the section defaults when the server omits them for a client', async () => {
    const { fixture, component } = await setup();

    await component.onClientChange('client-rt');
    fixture.detectChanges();

    expect(component.clientConfigs()[1].unlinkedConfig).not.toBeNull();
    expect(component.unlinkedModel()).toEqual({
      enabled: false,
      targetCategory: 'cleanuparr-unlinked',
      useTag: false,
      ignoredRootDirs: [],
      categories: [],
    });
    expect(component.deadTorrentModel()).toEqual({
      enabled: false,
      targetCategory: 'cleanuparr-dead',
      useTag: false,
      maxStrikes: 0,
      categories: [],
    });
    expect(component.orphanedFilesModel()).toEqual({
      enabled: false,
      scanDirectories: [],
      orphanedDirectory: '',
      excludePatterns: [],
      minFileAgeHours: 24,
      purgeAfterHours: null,
    });
    expect(component.unlinkedDirty()).toBe(false);
    expect(component.deadTorrentDirty()).toBe(false);
    expect(component.orphanedFilesDirty()).toBe(false);
  });

  it('gates the tag and dead torrent sections on the selected download client type', async () => {
    const { fixture, component } = await setup();

    expect(component.isSelectedClientQBittorrent()).toBe(true);
    expect(component.isTagFilterableClient()).toBe(true);
    expect(component.isSeedersFilterableClient()).toBe(true);
    expect(component.isDeadTorrentCapableClient()).toBe(true);
    expect(fixture.nativeElement.textContent).toContain('Dead Torrents');

    await component.onClientChange('client-rt');
    fixture.detectChanges();

    expect(component.isSelectedClientQBittorrent()).toBe(false);
    expect(component.isTagFilterableClient()).toBe(false);
    expect(component.isSeedersFilterableClient()).toBe(false);
    expect(component.isDeadTorrentCapableClient()).toBe(false);
    expect(fixture.nativeElement.textContent).not.toContain('Dead Torrents');
  });

  it('keeps the selection when the discard prompt is cancelled and drops the edits once confirmed', async () => {
    const { fixture, component, confirm } = await setup();

    component.unlinkedModel.update((m) => ({ ...m, categories: ['tv', 'movies'] }));
    fixture.detectChanges();
    expect(component.unlinkedDirty()).toBe(true);
    expect(component.hasPendingChanges()).toBe(true);

    const cancelled = component.onClientChange('client-rt');
    expect(confirm.state()?.title).toBe('Unsaved Changes');
    confirm.cancel();
    await cancelled;
    fixture.detectChanges();

    expect(component.selectedClientId()).toBe('client-qb');
    expect(component.unlinkedModel().categories).toEqual(['tv', 'movies']);

    const accepted = component.onClientChange('client-rt');
    confirm.accept();
    await accepted;
    fixture.detectChanges();

    expect(component.selectedClientId()).toBe('client-rt');

    await component.onClientChange('client-qb');
    fixture.detectChanges();

    expect(component.unlinkedModel().categories).toEqual(['tv']);
    expect(component.unlinkedDirty()).toBe(false);
  });

  it('saves the unlinked section with the edited payload and clears its dirty flag', async () => {
    const { fixture, component, api } = await setup();

    component.unlinkedModel.update((m) => ({ ...m, enabled: false, categories: ['movies'] }));
    fixture.detectChanges();
    expect(component.unlinkedDirty()).toBe(true);

    component.saveUnlinkedConfig();
    fixture.detectChanges();

    expect(api.updateUnlinkedConfig).toHaveBeenCalledWith('client-qb', {
      enabled: false,
      targetCategory: 'unlinked',
      useTag: true,
      ignoredRootDirs: ['/mnt/cross-seed'],
      categories: ['movies'],
    });
    expect(component.unlinkedDirty()).toBe(false);
    expect(component.unlinkedSaved()).toBe(true);
  });

  it('coerces the empty numeric fields when saving the dead torrent and orphaned files sections', async () => {
    const { fixture, component, api } = await setup();

    component.deadTorrentModel.update((m) => ({ ...m, maxStrikes: null }));
    component.orphanedFilesModel.update((m) => ({
      ...m,
      minFileAgeHours: null,
      purgeAfterHours: null,
    }));
    fixture.detectChanges();

    component.saveDeadTorrentConfig();
    component.saveOrphanedFilesConfig();
    fixture.detectChanges();

    expect(api.updateDeadTorrentConfig).toHaveBeenCalledWith('client-qb', {
      enabled: true,
      targetCategory: 'dead',
      useTag: false,
      maxStrikes: 0,
      categories: ['movies'],
    });
    expect(api.updateOrphanedFilesConfig).toHaveBeenCalledWith('client-qb', {
      enabled: true,
      scanDirectories: ['/data/torrents'],
      orphanedDirectory: '/data/orphaned',
      excludePatterns: ['*.nfo'],
      minFileAgeHours: 24,
      purgeAfterHours: undefined,
    });
    expect(component.deadTorrentDirty()).toBe(false);
    expect(component.orphanedFilesDirty()).toBe(false);
  });

  it('reorders the seeding rules locally and posts the new order', async () => {
    const { fixture, component, api } = await setup();

    component.onRulesReorder(dropEvent(0, 1));
    fixture.detectChanges();

    expect(component.selectedClient()?.seedingRules.map((r) => r.name)).toEqual([
      'TV cleanup',
      'Movies cleanup',
    ]);
    expect(api.reorderSeedingRules).toHaveBeenCalledWith('client-qb', ['rule-b', 'rule-a']);
  });

  it('deletes a seeding rule only once confirmed and reloads the list afterwards', async () => {
    const { fixture, component, api, confirm } = await setup();

    const cancelled = component.deleteRule(RULE_A);
    expect(confirm.state()?.message).toContain('Movies cleanup');
    confirm.cancel();
    await cancelled;

    expect(api.deleteSeedingRule).not.toHaveBeenCalled();

    const accepted = component.deleteRule(RULE_A);
    confirm.accept();
    await accepted;
    fixture.detectChanges();

    expect(api.deleteSeedingRule).toHaveBeenCalledWith('rule-a');
    expect(api.getSeedingRules).toHaveBeenCalledWith('client-qb');
    expect(component.selectedClient()?.seedingRules.map((r) => r.name)).toEqual(['TV cleanup']);
    expect(component.rulesReloading()).toBe(false);
  });

  it('opens the rule modal empty for a new rule and reloads the list when the modal saves', async () => {
    const { fixture, component, api } = await setup();

    component.openRuleModal(RULE_B);
    fixture.detectChanges();
    expect(component.editingRule()).toBe(RULE_B);
    expect(component.ruleModalVisible()).toBe(true);

    component.openRuleModal();
    fixture.detectChanges();
    expect(component.editingRule()).toBeNull();
    expect(component.ruleModalVisible()).toBe(true);

    component.onSeedingRuleSaved();
    fixture.detectChanges();

    expect(api.getSeedingRules).toHaveBeenCalledWith('client-qb');
    expect(component.selectedClient()?.seedingRules).toEqual([RULE_B]);
  });

  it('turns dirty after a global edit and posts the generated cron before going clean again', async () => {
    const { fixture, component, api } = await setup();

    expect(component.dirty()).toBe(false);

    component.dcForm.scheduleUnit().value.set(ScheduleUnit.Hours);
    fixture.detectChanges();

    expect(component.dcForm.scheduleEvery().value()).toBe(1);
    expect(component.dirty()).toBe(true);

    component.save();
    fixture.detectChanges();

    expect(api.updateConfig).toHaveBeenCalledWith({
      enabled: true,
      cronExpression: '0 0 0/1 ? * * *',
      useAdvancedScheduling: false,
      ignoredDownloads: ['skip-me'],
    });
    expect(component.dirty()).toBe(false);
    expect(component.saved()).toBe(true);
  });

  it('posts the raw cron expression when advanced scheduling is on', async () => {
    const { fixture, component, api } = await setup();

    component.dcForm.useAdvancedScheduling().value.set(true);
    component.dcForm.cronExpression().value.set('0 0 3 ? * *');
    fixture.detectChanges();

    component.save();

    expect(api.updateConfig).toHaveBeenCalledWith({
      enabled: true,
      cronExpression: '0 0 3 ? * *',
      useAdvancedScheduling: true,
      ignoredDownloads: ['skip-me'],
    });
  });
});
