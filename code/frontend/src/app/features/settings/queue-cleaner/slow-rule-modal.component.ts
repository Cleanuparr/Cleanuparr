import { Component, ChangeDetectionStrategy, inject, signal, effect, untracked, input, model, output } from '@angular/core';
import { form, required, min, max, maxLength, validate, disabled, FormField } from '@angular/forms/signals';
import {
  ModalComponent, InputComponent, ToggleComponent, NumberInputComponent,
  SelectComponent, SizeInputComponent, ButtonComponent,
  type SelectOption, type SizeUnit,
} from '@ui';
import { QueueCleanerApi } from '@core/api/queue-cleaner.api';
import { ToastService } from '@core/services/toast.service';
import { SlowRule, CreateSlowRuleDto } from '@shared/models/queue-rule.model';
import { TorrentPrivacyType } from '@shared/models/enums';

interface SlowRuleFormModel {
  name: string;
  enabled: boolean;
  maxStrikes: number | null;
  minSpeed: string;
  maxTimeHours: number | null;
  privacyType: TorrentPrivacyType;
  minCompletion: number | null;
  maxCompletion: number | null;
  ignoreAboveSize: string;
  resetOnProgress: boolean;
  deletePrivate: boolean;
  changeCategory: boolean;
}

const PRIVACY_TYPE_OPTIONS: SelectOption[] = [
  { label: 'Public', value: TorrentPrivacyType.Public },
  { label: 'Private', value: TorrentPrivacyType.Private },
  { label: 'Both', value: TorrentPrivacyType.Both },
];

@Component({
  selector: 'app-slow-rule-modal',
  standalone: true,
  imports: [
    ModalComponent, InputComponent, ToggleComponent, NumberInputComponent,
    SelectComponent, SizeInputComponent, ButtonComponent, FormField,
  ],
  templateUrl: './slow-rule-modal.component.html',
  styleUrl: './slow-rule-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SlowRuleModalComponent {
  private readonly api = inject(QueueCleanerApi);
  private readonly toast = inject(ToastService);

  readonly rule = input<SlowRule | null>(null);
  readonly visible = model(false);
  readonly saved = output<void>();

  readonly privacyTypeOptions = PRIVACY_TYPE_OPTIONS;
  readonly speedUnits: SizeUnit[] = [
    { label: 'KB/s', value: 'KB' },
    { label: 'MB/s', value: 'MB' },
  ];
  readonly sizeUnitsLarge: SizeUnit[] = [
    { label: 'MB', value: 'MB' },
    { label: 'GB', value: 'GB' },
  ];

  readonly saving = signal(false);

  private readonly defaults: SlowRuleFormModel = {
    name: '', enabled: true, maxStrikes: 3, minSpeed: '', maxTimeHours: 0,
    privacyType: TorrentPrivacyType.Both, minCompletion: 0, maxCompletion: 100,
    ignoreAboveSize: '', resetOnProgress: false, deletePrivate: false, changeCategory: false,
  };
  readonly model = signal<SlowRuleFormModel>({ ...this.defaults });
  readonly form = form(this.model, (p) => {
    required(p.name, { message: 'Name is required' });
    maxLength(p.name, 100, { message: 'Name cannot exceed 100 characters' });
    required(p.maxStrikes, { message: 'This field is required' });
    min(p.maxStrikes, 3, { message: 'Min value is 3' });
    max(p.maxStrikes, 5000, { message: 'Max value is 5000' });
    min(p.maxTimeHours, 0);
    min(p.minCompletion, 0);
    max(p.minCompletion, 100);
    min(p.maxCompletion, 1);
    max(p.maxCompletion, 100);
    validate(p.maxCompletion, ({ value, valueOf }) => {
      const minC = valueOf(p.minCompletion) ?? 0;
      const maxC = value() ?? 100;
      if (maxC <= 0) return { kind: 'completion', message: 'Max percentage must be greater than 0' };
      if (maxC < minC) return { kind: 'completion', message: 'Max percentage must be greater than or equal to Min percentage' };
      return undefined;
    });
    disabled(p.deletePrivate, () => this.model().privacyType === TorrentPrivacyType.Public);
  });

  constructor() {
    // Populate the form from the input rule (or defaults) each time the modal opens.
    effect(() => {
      if (!this.visible()) {
        return;
      }
      const r = untracked(() => this.rule());
      untracked(() => this.model.set(r ? {
        name: r.name,
        enabled: r.enabled,
        maxStrikes: r.maxStrikes,
        minSpeed: r.minSpeed,
        maxTimeHours: r.maxTimeHours,
        privacyType: r.privacyType,
        minCompletion: r.minCompletionPercentage,
        maxCompletion: r.maxCompletionPercentage,
        ignoreAboveSize: r.ignoreAboveSize ?? '',
        resetOnProgress: r.resetStrikesOnProgress,
        deletePrivate: r.deletePrivateTorrentsFromClient,
        changeCategory: r.changeCategory ?? false,
      } : { ...this.defaults }));
    });

    // Guard on the current value: model.update always creates a new object, so an
    // unconditional write would re-trigger this effect forever.
    effect(() => {
      const m = this.model();
      if ((m.changeCategory || m.privacyType === TorrentPrivacyType.Public) && m.deletePrivate) {
        untracked(() => this.model.update(s => ({ ...s, deletePrivate: false })));
      }
    });
  }

  save(): void {
    if (this.form().invalid()) {
      return;
    }

    const m = this.model();
    const changeCategory = m.changeCategory;
    const dto: CreateSlowRuleDto = {
      name: m.name.trim(),
      enabled: m.enabled,
      maxStrikes: m.maxStrikes ?? 3,
      privacyType: m.privacyType,
      minCompletionPercentage: m.minCompletion ?? 0,
      maxCompletionPercentage: m.maxCompletion ?? 100,
      resetStrikesOnProgress: m.resetOnProgress,
      minSpeed: m.minSpeed.trim(),
      maxTimeHours: m.maxTimeHours ?? 0,
      ignoreAboveSize: m.ignoreAboveSize.trim() || undefined,
      deletePrivateTorrentsFromClient: changeCategory ? false : m.deletePrivate,
      changeCategory,
    };

    const editing = this.rule();
    const request = editing?.id
      ? this.api.updateSlowRule(editing.id, dto)
      : this.api.createSlowRule(dto);

    this.saving.set(true);
    request.subscribe({
      next: () => {
        this.toast.success(editing ? 'Slow rule updated' : 'Slow rule created');
        this.saving.set(false);
        this.visible.set(false);
        this.saved.emit();
      },
      error: (e: Error) => {
        this.toast.error(e.message);
        this.saving.set(false);
      },
    });
  }
}
