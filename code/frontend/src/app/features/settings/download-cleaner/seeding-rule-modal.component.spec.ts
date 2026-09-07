import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { DownloadCleanerApi } from '@core/api/download-cleaner.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { SeedingRule } from '@shared/models/download-cleaner-config.model';
import { SeedingRuleAction, TorrentPrivacyType } from '@shared/models/enums';
import { SeedingRuleModalComponent } from './seeding-rule-modal.component';

const RULE: SeedingRule = {
  id: 'rule-a',
  name: 'Movies cleanup',
  categories: ['movies'],
  trackerPatterns: ['tracker.example'],
  tagsAny: ['keep'],
  tagsAll: ['seeded'],
  priority: 0,
  privacyType: TorrentPrivacyType.Private,
  maxRatio: 2,
  minSeedTime: 24,
  maxSeedTime: 240,
  minSeeders: 3,
  maxInactiveDays: 7,
  action: SeedingRuleAction.Delete,
  deleteSourceFiles: false,
};

@Component({
  imports: [SeedingRuleModalComponent],
  template: `<app-seeding-rule-modal
    [rule]="rule()"
    [(visible)]="visible"
    [clientId]="clientId()"
    [isTagFilterableClient]="isTagFilterableClient()"
    [isSelectedClientTransmission]="isSelectedClientTransmission()"
    [isSeedersFilterableClient]="isSeedersFilterableClient()"
    [isInactivityFilterableClient]="isInactivityFilterableClient()"
    (saved)="savedCount = savedCount + 1"
  />`,
})
class HostComponent {
  readonly rule = signal<SeedingRule | null>(null);
  readonly visible = signal(false);
  readonly clientId = signal<string | null>('client-qb');
  readonly isTagFilterableClient = signal(false);
  readonly isSelectedClientTransmission = signal(false);
  readonly isSeedersFilterableClient = signal(false);
  readonly isInactivityFilterableClient = signal(false);
  savedCount = 0;
}

function createApi() {
  return {
    createSeedingRule: vi.fn(() => of(RULE)),
    updateSeedingRule: vi.fn(() => of(RULE)),
  };
}

interface Setup {
  fixture: ComponentFixture<HostComponent>;
  host: HostComponent;
  modal: SeedingRuleModalComponent;
  api: ReturnType<typeof createApi>;
  toast: ToastService;
}

describe('SeedingRuleModalComponent', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  function setup(rule: SeedingRule | null = null): Setup {
    const api = createApi();
    TestBed.configureTestingModule({
      providers: [{ provide: DownloadCleanerApi, useValue: api }],
    });

    const fixture = TestBed.createComponent(HostComponent);
    const host = fixture.componentInstance;
    host.rule.set(rule);
    host.visible.set(true);
    fixture.detectChanges();

    return {
      fixture,
      host,
      modal: fixture.debugElement.children[0].componentInstance,
      api,
      toast: TestBed.inject(ToastService),
    };
  }

  function text(fixture: ComponentFixture<HostComponent>): string {
    return fixture.nativeElement.textContent as string;
  }

  function numberField(fixture: ComponentFixture<HostComponent>, label: string): HTMLInputElement {
    const inputs: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('app-number-input'));
    const match = inputs.find(el => el.querySelector('.number-label')?.textContent?.trim().startsWith(label));
    if (!match) {
      throw new Error(`No number input labelled "${label}"`);
    }
    return match.querySelector('.number-field') as HTMLInputElement;
  }

  function footerButton(fixture: ComponentFixture<HostComponent>, label: string): HTMLButtonElement {
    const buttons: HTMLElement[] = Array.from(fixture.nativeElement.querySelectorAll('[modal-footer] app-button'));
    const match = buttons.find(el => el.textContent?.trim() === label);
    if (!match) {
      throw new Error(`No footer button labelled "${label}"`);
    }
    return match.querySelector('button') as HTMLButtonElement;
  }

  function type(fixture: ComponentFixture<HostComponent>, field: HTMLInputElement, value: string): void {
    field.value = value;
    field.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  }

  it('populates the form from the rule it was opened with and reports it clean', () => {
    const { modal } = setup(RULE);

    expect(modal.model()).toEqual({
      name: 'Movies cleanup',
      categories: ['movies'],
      trackerPatterns: ['tracker.example'],
      tagsAny: ['keep'],
      tagsAll: ['seeded'],
      privacyType: TorrentPrivacyType.Private,
      maxRatio: 2,
      minSeedTime: 24,
      maxSeedTime: 240,
      minSeeders: 3,
      maxInactiveDays: 7,
      action: SeedingRuleAction.Delete,
      deleteSourceFiles: false,
    });
    expect(modal.hasPendingChanges()).toBe(false);
  });

  it('falls back to the defaults when opened without a rule', () => {
    const { fixture, modal } = setup();

    expect(modal.model()).toEqual({
      name: '', categories: [], trackerPatterns: [], tagsAny: [], tagsAll: [],
      privacyType: TorrentPrivacyType.Public, maxRatio: -1, minSeedTime: 0,
      maxSeedTime: -1, minSeeders: 0, maxInactiveDays: -1, action: SeedingRuleAction.Delete,
      deleteSourceFiles: true,
    });
    expect(modal.hasPendingChanges()).toBe(false);
    expect(text(fixture)).toContain('Add Seeding Rule');
  });

  it('substitutes server-omitted lists and values with their defaults', () => {
    const { modal } = setup({
      ...RULE,
      categories: undefined as unknown as string[],
      trackerPatterns: undefined as unknown as string[],
      tagsAny: undefined,
      tagsAll: undefined,
      minSeeders: undefined,
      maxInactiveDays: undefined,
    });

    expect(modal.model()).toMatchObject({
      categories: [], trackerPatterns: [], tagsAny: [], tagsAll: [],
      minSeeders: 0, maxInactiveDays: -1,
    });
  });

  it('hides Min Seed Time while max ratio is disabled and shows it once a ratio is set', () => {
    const { fixture, modal } = setup(RULE);

    expect(modal.isMaxRatioEnabled()).toBe(true);
    expect(text(fixture)).toContain('Min Seed Time');

    type(fixture, numberField(fixture, 'Max Ratio'), '-1');

    expect(modal.isMaxRatioEnabled()).toBe(false);
    expect(text(fixture)).not.toContain('Min Seed Time');

    type(fixture, numberField(fixture, 'Max Ratio'), '0');

    expect(modal.isMaxRatioEnabled()).toBe(true);
    expect(text(fixture)).toContain('Min Seed Time');
  });

  it('treats a cleared max ratio as disabled', () => {
    const { fixture, modal } = setup(RULE);

    type(fixture, numberField(fixture, 'Max Ratio'), '');

    expect(modal.model().maxRatio).toBeNull();
    expect(modal.isMaxRatioEnabled()).toBe(false);
    expect(text(fixture)).not.toContain('Min Seed Time');

    type(fixture, numberField(fixture, 'Max Seed Time'), '');

    expect(modal.disabledError()).toBe('Both max ratio and max seed time cannot be disabled at the same time');
  });

  it('hides Delete Source Files unless the action deletes', () => {
    const { fixture, modal } = setup(RULE);

    expect(text(fixture)).toContain('Delete Source Files');

    modal.model.update(m => ({ ...m, action: SeedingRuleAction.Stop }));
    fixture.detectChanges();

    expect(modal.isDeleteAction()).toBe(false);
    expect(text(fixture)).not.toContain('Delete Source Files');
  });

  it('shows the client-specific fields only for clients that support them', () => {
    const { fixture, host } = setup(RULE);

    expect(text(fixture)).not.toContain('Tags (Any)');
    expect(text(fixture)).not.toContain('Min Seeders');
    expect(text(fixture)).not.toContain('Max Inactive Days');

    host.isTagFilterableClient.set(true);
    host.isSeedersFilterableClient.set(true);
    host.isInactivityFilterableClient.set(true);
    fixture.detectChanges();

    expect(text(fixture)).toContain('Tags (Any)');
    expect(text(fixture)).toContain('Tags (All)');
    expect(text(fixture)).toContain('Min Seeders');
    expect(text(fixture)).toContain('Max Inactive Days');

    host.isSelectedClientTransmission.set(true);
    fixture.detectChanges();

    expect(text(fixture)).toContain('Labels (Any)');
    expect(text(fixture)).toContain('Labels (All)');
  });

  it('reports both removal conditions being disabled and refuses to save', () => {
    const { fixture, modal, api } = setup(RULE);

    type(fixture, numberField(fixture, 'Max Ratio'), '-1');
    type(fixture, numberField(fixture, 'Max Seed Time'), '-1');

    expect(modal.disabledError()).toBe('Both max ratio and max seed time cannot be disabled at the same time');
    expect(fixture.nativeElement.querySelector('.category-error').textContent).toContain(
      'Both max ratio and max seed time cannot be disabled at the same time');

    modal.save();

    expect(api.updateSeedingRule).not.toHaveBeenCalled();
  });

  it('creates a trimmed rule for the selected client, then closes and reports the save', () => {
    const { fixture, host, modal, api, toast } = setup();

    modal.model.update(m => ({
      ...m,
      name: '  Movies cleanup  ',
      categories: [' movies ', ''],
      trackerPatterns: [' tracker.example '],
      maxRatio: 2,
    }));
    fixture.detectChanges();

    footerButton(fixture, 'Create').click();
    fixture.detectChanges();

    expect(api.createSeedingRule).toHaveBeenCalledWith('client-qb', {
      name: 'Movies cleanup',
      categories: ['movies'],
      trackerPatterns: ['tracker.example'],
      tagsAny: [],
      tagsAll: [],
      privacyType: TorrentPrivacyType.Public,
      maxRatio: 2,
      minSeedTime: 0,
      maxSeedTime: -1,
      minSeeders: 0,
      maxInactiveDays: -1,
      action: SeedingRuleAction.Delete,
      deleteSourceFiles: true,
    });
    expect(toast.toasts()[0]).toMatchObject({ severity: 'success', message: 'Seeding rule created' });
    expect(modal.saving()).toBe(false);
    expect(host.visible()).toBe(false);
    expect(host.savedCount).toBe(1);
  });

  it('updates the rule it was opened with instead of creating another one', () => {
    const { fixture, host, api, toast } = setup(RULE);

    footerButton(fixture, 'Update').click();
    fixture.detectChanges();

    expect(api.createSeedingRule).not.toHaveBeenCalled();
    expect(api.updateSeedingRule).toHaveBeenCalledWith('rule-a', expect.objectContaining({ name: 'Movies cleanup' }));
    expect(toast.toasts()[0]).toMatchObject({ severity: 'success', message: 'Seeding rule updated' });
    expect(host.visible()).toBe(false);
  });

  it('sends the defaults for every value the model has cleared', () => {
    const { fixture, api, modal } = setup(RULE);

    modal.model.update(m => ({ ...m, maxRatio: null, minSeedTime: null, minSeeders: null, maxInactiveDays: null }));
    fixture.detectChanges();

    modal.save();

    expect(api.updateSeedingRule).toHaveBeenCalledWith('rule-a', expect.objectContaining({
      maxRatio: -1, minSeedTime: 0, minSeeders: 0, maxInactiveDays: -1,
    }));

    modal.model.update(m => ({ ...m, maxRatio: 2, maxSeedTime: null }));
    fixture.detectChanges();

    modal.save();

    expect(api.updateSeedingRule).toHaveBeenLastCalledWith('rule-a', expect.objectContaining({ maxSeedTime: -1 }));
  });

  it('keeps the modal open and surfaces the server message when the save is rejected', () => {
    const { fixture, host, modal, api, toast } = setup(RULE);
    const error = new ApiError('Name already used');
    error.statusCode = 400;
    api.updateSeedingRule.mockReturnValue(throwError(() => error));

    modal.save();
    fixture.detectChanges();

    expect(toast.toasts()[0]).toMatchObject({ severity: 'error', message: 'Name already used' });
    expect(modal.saving()).toBe(false);
    expect(host.visible()).toBe(true);
    expect(host.savedCount).toBe(0);
  });

  it('reports a generic failure for a non-validation error', () => {
    const { fixture, modal, api, toast } = setup(RULE);
    const error = new ApiError('boom');
    error.statusCode = 500;
    api.updateSeedingRule.mockReturnValue(throwError(() => error));

    modal.save();
    fixture.detectChanges();

    expect(toast.toasts()[0]).toMatchObject({ severity: 'error', message: 'Failed to save seeding rule' });
  });

  it('refuses to save an incomplete form or one without a client', () => {
    const { fixture, host, modal, api } = setup();

    modal.save();

    expect(api.createSeedingRule).not.toHaveBeenCalled();

    modal.model.update(m => ({ ...m, name: 'Movies cleanup', categories: ['movies'], maxRatio: 2 }));
    host.clientId.set(null);
    fixture.detectChanges();

    modal.save();

    expect(api.createSeedingRule).not.toHaveBeenCalled();
  });

  it('refuses to save while a chip input still holds uncommitted text', () => {
    const { fixture, modal, api } = setup(RULE);

    const chip = fixture.nativeElement.querySelector('app-chip-input .chip-input') as HTMLInputElement;
    chip.value = 'movies-4k';
    chip.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(modal.hasUncommittedInputs()).toBe(true);
    expect(footerButton(fixture, 'Update').disabled).toBe(true);

    modal.save();

    expect(api.updateSeedingRule).not.toHaveBeenCalled();
  });

  it('closes without saving when cancelled', () => {
    const { fixture, host, api } = setup(RULE);

    footerButton(fixture, 'Cancel').click();
    fixture.detectChanges();

    expect(host.visible()).toBe(false);
    expect(api.updateSeedingRule).not.toHaveBeenCalled();
  });

  it('propagates a dismissal from the modal shell back to the caller', () => {
    const { fixture, host, api } = setup(RULE);

    fixture.nativeElement.querySelector('.modal__close').click();
    fixture.detectChanges();

    expect(host.visible()).toBe(false);
    expect(api.updateSeedingRule).not.toHaveBeenCalled();
  });

  it('tracks pending changes only while the modal is open', () => {
    const { fixture, host, modal } = setup(RULE);

    modal.model.update(m => ({ ...m, name: 'Renamed' }));
    fixture.detectChanges();

    expect(modal.hasPendingChanges()).toBe(true);

    host.visible.set(false);
    fixture.detectChanges();

    expect(modal.hasPendingChanges()).toBe(false);
  });
});
