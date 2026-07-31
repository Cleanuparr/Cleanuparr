import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { of } from 'rxjs';
import { QueueCleanerApi } from '@core/api/queue-cleaner.api';
import { ConfirmService } from '@core/services/confirm.service';
import { QueueCleanerConfig } from '@shared/models/queue-cleaner-config.model';
import { SlowRule, StallRule } from '@shared/models/queue-rule.model';
import { PatternMode, ScheduleUnit, TorrentPrivacyType } from '@shared/models/enums';
import { QueueCleanerComponent } from './queue-cleaner.component';

const CONFIG: QueueCleanerConfig = {
  enabled: true,
  cronExpression: '0 0/10 * ? * * *',
  useAdvancedScheduling: false,
  ignoredDownloads: ['ignored-hash'],
  processNoContentId: true,
  failedImport: {
    maxStrikes: 4,
    ignorePrivate: false,
    deletePrivate: true,
    skipIfNotFoundInClient: true,
    patterns: ['unpack'],
    changeCategory: false,
  },
  downloadingMetadataMaxStrikes: 6,
};

const STALL_RULES: StallRule[] = [
  {
    id: 'stall-1',
    name: 'Everything stalled',
    enabled: true,
    maxStrikes: 3,
    privacyType: TorrentPrivacyType.Both,
    minCompletionPercentage: 0,
    maxCompletionPercentage: 100,
    deletePrivateTorrentsFromClient: false,
    changeCategory: false,
    resetStrikesOnProgress: true,
  },
];

const SLOW_RULES: SlowRule[] = [
  {
    id: 'slow-1',
    name: 'Early slow downloads',
    enabled: true,
    maxStrikes: 5,
    privacyType: TorrentPrivacyType.Both,
    minCompletionPercentage: 0,
    maxCompletionPercentage: 50,
    deletePrivateTorrentsFromClient: false,
    changeCategory: false,
    resetStrikesOnProgress: false,
    minSpeed: '1MB',
    maxTimeHours: 0,
    ignoreWhileAltSpeedActive: false,
  },
];

function createApi(config: QueueCleanerConfig, stall: StallRule[], slow: SlowRule[]) {
  const state = { stall, slow };
  return {
    state,
    getConfig: vi.fn(() => of(config)),
    updateConfig: vi.fn(() => of(undefined)),
    getStallRules: vi.fn(() => of(state.stall)),
    getSlowRules: vi.fn(() => of(state.slow)),
    deleteStallRule: vi.fn(() => of(undefined)),
    deleteSlowRule: vi.fn(() => of(undefined)),
  };
}

interface Setup {
  fixture: ComponentFixture<QueueCleanerComponent>;
  component: QueueCleanerComponent;
  api: ReturnType<typeof createApi>;
  confirm: ConfirmService;
}

describe('QueueCleanerComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setup(
    config: QueueCleanerConfig = CONFIG,
    stall: StallRule[] = STALL_RULES,
    slow: SlowRule[] = SLOW_RULES,
  ): Setup {
    const api = createApi(config, stall, slow);

    TestBed.configureTestingModule({
      providers: [provideHttpClient(), { provide: QueueCleanerApi, useValue: api }],
    });

    const fixture = TestBed.createComponent(QueueCleanerComponent);
    fixture.detectChanges();

    return {
      fixture,
      component: fixture.componentInstance,
      api,
      confirm: TestBed.inject(ConfirmService),
    };
  }

  it('loads the config into the form, parses the cron and defaults the pattern mode', () => {
    const { component } = setup();

    expect(component.qcForm.enabled().value()).toBe(true);
    expect(component.qcForm.processNoContentId().value()).toBe(true);
    expect(component.qcForm.scheduleUnit().value()).toBe(ScheduleUnit.Minutes);
    expect(component.qcForm.scheduleEvery().value()).toBe(10);
    expect(component.qcForm.ignoredDownloads().value()).toEqual(['ignored-hash']);
    expect(component.qcForm.failedMaxStrikes().value()).toBe(4);
    expect(component.qcForm.failedPatterns().value()).toEqual(['unpack']);
    expect(component.qcForm.failedPatternMode().value()).toBe(PatternMode.Exclude);
    expect(component.qcForm.metadataMaxStrikes().value()).toBe(6);
    expect(component.dirty()).toBe(false);
    expect(component.hasPendingChanges()).toBe(false);
  });

  it('disables the failed import sub-fields and stops requiring patterns when max strikes is zero', () => {
    const { fixture, component } = setup();

    component.qcForm.failedMaxStrikes().value.set(0);
    component.qcForm.failedPatternMode().value.set(PatternMode.Include);
    component.qcForm.failedPatterns().value.set([]);
    fixture.detectChanges();

    expect(component.failedSubFieldsDisabled()).toBe(true);
    expect(component.qcForm.failedIgnorePrivate().disabled()).toBe(true);
    expect(component.qcForm.failedChangeCategory().disabled()).toBe(true);
    expect(component.qcForm.failedSkipNotFound().disabled()).toBe(true);
    expect(component.qcForm.failedPatternMode().disabled()).toBe(true);
    expect(component.qcForm.failedPatterns().disabled()).toBe(true);
    expect(component.qcForm.failedPatterns().errors()).toEqual([]);

    component.qcForm.failedMaxStrikes().value.set(4);
    fixture.detectChanges();

    expect(component.failedSubFieldsDisabled()).toBe(false);
    expect(component.qcForm.failedPatterns().errors()[0]?.message).toBe(
      'At least one pattern is required when using Include mode',
    );
  });

  it('clears delete private whenever ignore private or change category is turned on', () => {
    const { fixture, component } = setup();

    expect(component.qcForm.failedDeletePrivate().value()).toBe(true);

    component.qcForm.failedIgnorePrivate().value.set(true);
    fixture.detectChanges();

    expect(component.qcForm.failedDeletePrivate().value()).toBe(false);
    expect(component.failedDeletePrivateDisabled()).toBe(true);

    component.qcForm.failedIgnorePrivate().value.set(false);
    component.qcForm.failedDeletePrivate().value.set(true);
    fixture.detectChanges();
    expect(component.qcForm.failedDeletePrivate().value()).toBe(true);

    component.qcForm.failedChangeCategory().value.set(true);
    fixture.detectChanges();

    expect(component.qcForm.failedDeletePrivate().value()).toBe(false);
  });

  it('swaps the pattern label and hint with the pattern mode', () => {
    const { fixture, component } = setup();

    expect(component.patternLabel()).toBe('Excluded Patterns');
    expect(component.patternHint()).toContain('will be skipped');

    component.qcForm.failedPatternMode().value.set(PatternMode.Include);
    fixture.detectChanges();

    expect(component.patternLabel()).toBe('Included Patterns');
    expect(component.patternHint()).toContain('Only failed imports');
  });

  it('warns about coverage gaps only for the rule list that leaves ranges uncovered', () => {
    const { fixture, component } = setup();

    expect(component.stallCoverage().hasGaps).toBe(false);
    expect(component.slowCoverage().gaps).toEqual([
      { privacyType: TorrentPrivacyType.Public, from: 50, to: 100 },
      { privacyType: TorrentPrivacyType.Private, from: 50, to: 100 },
    ]);

    component.stallExpanded.set(true);
    component.slowExpanded.set(true);
    fixture.detectChanges();

    const warnings = fixture.nativeElement.querySelectorAll('.coverage-warning');
    expect(warnings).toHaveLength(1);
    expect((warnings[0] as HTMLElement).textContent).toContain('50% - 100% completion not covered');
  });

  it('renders the loaded rules and opens the modals for a new and an existing rule', () => {
    const { fixture, component } = setup();

    component.stallExpanded.set(true);
    component.slowExpanded.set(true);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Everything stalled');
    expect(fixture.nativeElement.textContent).toContain('Early slow downloads');

    component.openStallModal(STALL_RULES[0]);
    fixture.detectChanges();
    expect(component.editingStallRule()).toBe(STALL_RULES[0]);
    expect(component.stallModalVisible()).toBe(true);

    component.stallModalVisible.set(false);
    component.openSlowModal();
    fixture.detectChanges();
    expect(component.editingSlowRule()).toBeNull();
    expect(component.slowModalVisible()).toBe(true);
  });

  it('refetches the stall rules when the modal reports a save', () => {
    const { fixture, component, api } = setup();

    api.state.stall = [];
    component.reloadStallRules();
    fixture.detectChanges();

    expect(api.getStallRules).toHaveBeenCalledTimes(2);
    expect(component.stallRules()).toEqual([]);
  });

  it('deletes a slow rule only once confirmed and reloads the list afterwards', async () => {
    const { fixture, component, api, confirm } = setup();

    const cancelled = component.deleteSlowRule(SLOW_RULES[0]);
    expect(confirm.state()?.message).toContain('Early slow downloads');
    confirm.cancel();
    await cancelled;

    expect(api.deleteSlowRule).not.toHaveBeenCalled();

    api.state.slow = [];
    const accepted = component.deleteSlowRule(SLOW_RULES[0]);
    confirm.accept();
    await accepted;
    fixture.detectChanges();

    expect(api.deleteSlowRule).toHaveBeenCalledWith('slow-1');
    expect(component.slowRules()).toEqual([]);
  });

  it('turns dirty after an edit and posts the generated cron before going clean again', () => {
    const { fixture, component, api } = setup();

    component.qcForm.scheduleEvery().value.set(20);
    component.qcForm.metadataMaxStrikes().value.set(9);
    fixture.detectChanges();

    expect(component.dirty()).toBe(true);

    component.save();
    fixture.detectChanges();

    expect(api.updateConfig).toHaveBeenCalledWith({
      enabled: true,
      cronExpression: '0 0/20 * ? * * *',
      useAdvancedScheduling: false,
      ignoredDownloads: ['ignored-hash'],
      processNoContentId: true,
      failedImport: {
        maxStrikes: 4,
        ignorePrivate: false,
        deletePrivate: true,
        skipIfNotFoundInClient: true,
        patterns: ['unpack'],
        patternMode: PatternMode.Exclude,
        changeCategory: false,
      },
      downloadingMetadataMaxStrikes: 9,
    });
    expect(component.dirty()).toBe(false);
    expect(component.saved()).toBe(true);
  });

  it('falls back to three strikes on empty inputs and never deletes private when changing category', () => {
    const { fixture, component, api } = setup();

    component.qcForm.failedChangeCategory().value.set(true);
    component.qcForm.failedMaxStrikes().value.set(null);
    component.qcForm.metadataMaxStrikes().value.set(null);
    fixture.detectChanges();

    component.save();

    expect(api.updateConfig).toHaveBeenCalledWith(
      expect.objectContaining({
        failedImport: expect.objectContaining({
          maxStrikes: 3,
          deletePrivate: false,
          changeCategory: true,
        }),
        downloadingMetadataMaxStrikes: 3,
      }),
    );
  });
});
