import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, input, model, output, viewChildren } from '@angular/core';
import { form, required, min, minLength, validate, FormField } from '@angular/forms/signals';
import {
  ModalComponent, InputComponent, SelectComponent, ChipInputComponent,
  NumberInputComponent, ToggleComponent, ButtonComponent,
  type SelectOption,
} from '@ui';
import { DownloadCleanerApi } from '@core/api/download-cleaner.api';
import { ApiError } from '@core/interceptors/error.interceptor';
import { ToastService } from '@core/services/toast.service';
import { SeedingRule } from '@shared/models/download-cleaner-config.model';
import { TorrentPrivacyType } from '@shared/models/enums';

interface SeedingRuleFormModel {
  name: string;
  categories: string[];
  trackerPatterns: string[];
  tagsAny: string[];
  tagsAll: string[];
  privacyType: TorrentPrivacyType;
  maxRatio: number | null;
  minSeedTime: number | null;
  maxSeedTime: number | null;
  minSeeders: number | null;
  deleteSourceFiles: boolean;
}

const PRIVACY_TYPE_OPTIONS: SelectOption[] = [
  { label: 'Public', value: TorrentPrivacyType.Public },
  { label: 'Private', value: TorrentPrivacyType.Private },
  { label: 'Both', value: TorrentPrivacyType.Both },
];

@Component({
  selector: 'app-seeding-rule-modal',
  standalone: true,
  imports: [
    ModalComponent, InputComponent, SelectComponent, ChipInputComponent,
    NumberInputComponent, ToggleComponent, ButtonComponent, FormField,
  ],
  templateUrl: './seeding-rule-modal.component.html',
  styleUrl: './seeding-rule-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SeedingRuleModalComponent {
  private readonly api = inject(DownloadCleanerApi);
  private readonly toast = inject(ToastService);

  readonly rule = input<SeedingRule | null>(null);
  readonly visible = model(false);
  readonly clientId = input<string | null>(null);
  readonly isTagFilterableClient = input(false);
  readonly isSelectedClientTransmission = input(false);
  readonly isSeedersFilterableClient = input(false);
  readonly saved = output<void>();

  readonly privacyTypeOptions = PRIVACY_TYPE_OPTIONS;

  private readonly ruleChipInputs = viewChildren<ChipInputComponent>('ruleChipInput');
  readonly hasUncommittedInputs = computed(() =>
    this.ruleChipInputs().some(c => c.hasUncommittedInput())
  );

  readonly saving = signal(false);

  /** JSON snapshot of the model as loaded when the modal opened, for dirty tracking. */
  private readonly openSnapshot = signal('');
  readonly hasPendingChanges = computed(() =>
    this.visible() && JSON.stringify(this.model()) !== this.openSnapshot());

  private readonly defaults: SeedingRuleFormModel = {
    name: '', categories: [], trackerPatterns: [], tagsAny: [], tagsAll: [],
    privacyType: TorrentPrivacyType.Public, maxRatio: -1, minSeedTime: 0,
    maxSeedTime: -1, minSeeders: 0, deleteSourceFiles: true,
  };
  readonly model = signal<SeedingRuleFormModel>({ ...this.defaults });
  readonly form = form(this.model, (p) => {
    required(p.name, { message: 'Name is required' });
    minLength(p.categories, 1, { message: 'At least one category is required' });
    min(p.maxRatio, -1);
    min(p.minSeedTime, 0);
    min(p.maxSeedTime, -1);
    min(p.minSeeders, 0);
    validate(p.maxSeedTime, () => {
      const m = this.model();
      return (m.maxRatio ?? -1) < 0 && (m.maxSeedTime ?? -1) < 0
        ? { kind: 'disabled', message: 'Both max ratio and max seed time cannot be disabled at the same time' }
        : undefined;
    });
  });

  readonly disabledError = computed(() =>
    this.form.maxSeedTime().errors().find(e => e.kind === 'disabled')?.message
  );

  constructor() {
    // Populate the form from the input rule (or defaults) each time the modal opens.
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const r = untracked(() => this.rule());
      untracked(() => {
        const next: SeedingRuleFormModel = r ? {
          name: r.name,
          categories: [...(r.categories ?? [])],
          trackerPatterns: [...(r.trackerPatterns ?? [])],
          tagsAny: [...(r.tagsAny ?? [])],
          tagsAll: [...(r.tagsAll ?? [])],
          privacyType: r.privacyType,
          maxRatio: r.maxRatio,
          minSeedTime: r.minSeedTime,
          maxSeedTime: r.maxSeedTime,
          minSeeders: r.minSeeders ?? 0,
          deleteSourceFiles: r.deleteSourceFiles,
        } : { ...this.defaults };
        this.model.set(next);
        this.openSnapshot.set(JSON.stringify(next));
      });
    });
  }

  save(): void {
    if (this.form().invalid() || this.hasUncommittedInputs()) {
      return;
    }
    const clientId = this.clientId();
    if (!clientId) {
      return;
    }

    const m = this.model();
    const sanitize = (list: string[]) => list.map(s => s.trim()).filter(s => s.length > 0);

    const dto: Partial<SeedingRule> = {
      name: m.name.trim(),
      categories: sanitize(m.categories),
      trackerPatterns: sanitize(m.trackerPatterns),
      tagsAny: sanitize(m.tagsAny),
      tagsAll: sanitize(m.tagsAll),
      privacyType: m.privacyType,
      maxRatio: m.maxRatio ?? -1,
      minSeedTime: m.minSeedTime ?? 0,
      maxSeedTime: m.maxSeedTime ?? -1,
      minSeeders: m.minSeeders ?? 0,
      deleteSourceFiles: m.deleteSourceFiles,
    };

    const editing = this.rule();
    const request = editing?.id
      ? this.api.updateSeedingRule(editing.id, dto)
      : this.api.createSeedingRule(clientId, dto);

    this.saving.set(true);
    request.subscribe({
      next: () => {
        this.toast.success(editing ? 'Seeding rule updated' : 'Seeding rule created');
        this.saving.set(false);
        this.visible.set(false);
        this.saved.emit();
      },
      error: (e: ApiError) => {
        this.toast.error(e.statusCode === 400 ? e.message : 'Failed to save seeding rule');
        this.saving.set(false);
      },
    });
  }
}
