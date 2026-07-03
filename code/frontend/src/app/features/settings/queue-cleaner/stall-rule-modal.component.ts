import { Component, ChangeDetectionStrategy, inject, signal, computed, effect, untracked, input, model, output } from '@angular/core';
import { form, required, min, max, maxLength, validate, disabled, FormField } from '@angular/forms/signals';
import {
  ModalComponent, InputComponent, ToggleComponent, NumberInputComponent,
  SelectComponent, SizeInputComponent, ButtonComponent,
  type SelectOption, type SizeUnit,
} from '@ui';
import { QueueCleanerApi } from '@core/api/queue-cleaner.api';
import { ToastService } from '@core/services/toast.service';
import { StallRule, CreateStallRuleDto } from '@shared/models/queue-rule.model';
import { TorrentPrivacyType } from '@shared/models/enums';

interface StallRuleFormModel {
  name: string;
  enabled: boolean;
  maxStrikes: number | null;
  privacyType: TorrentPrivacyType;
  minCompletion: number | null;
  maxCompletion: number | null;
  resetOnProgress: boolean;
  minProgress: string;
  deletePrivate: boolean;
  changeCategory: boolean;
}

const PRIVACY_TYPE_OPTIONS: SelectOption[] = [
  { label: 'Public', value: TorrentPrivacyType.Public },
  { label: 'Private', value: TorrentPrivacyType.Private },
  { label: 'Both', value: TorrentPrivacyType.Both },
];

@Component({
  selector: 'app-stall-rule-modal',
  standalone: true,
  imports: [
    ModalComponent, InputComponent, ToggleComponent, NumberInputComponent,
    SelectComponent, SizeInputComponent, ButtonComponent, FormField,
  ],
  templateUrl: './stall-rule-modal.component.html',
  styleUrl: './stall-rule-modal.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class StallRuleModalComponent {
  private readonly api = inject(QueueCleanerApi);
  private readonly toast = inject(ToastService);

  readonly rule = input<StallRule | null>(null);
  readonly visible = model(false);
  readonly saved = output<void>();

  readonly privacyTypeOptions = PRIVACY_TYPE_OPTIONS;
  readonly sizeUnits: SizeUnit[] = [
    { label: 'KB', value: 'KB' },
    { label: 'MB', value: 'MB' },
  ];

  readonly saving = signal(false);

  /** JSON snapshot of the model as loaded when the modal opened, for dirty tracking. */
  private readonly openSnapshot = signal('');
  readonly hasPendingChanges = computed(() =>
    this.visible() && JSON.stringify(this.model()) !== this.openSnapshot());

  private readonly defaults: StallRuleFormModel = {
    name: '', enabled: true, maxStrikes: 3, privacyType: TorrentPrivacyType.Both,
    minCompletion: 0, maxCompletion: 100, resetOnProgress: false, minProgress: '',
    deletePrivate: false, changeCategory: false,
  };
  readonly model = signal<StallRuleFormModel>({ ...this.defaults });
  readonly form = form(this.model, (p) => {
    required(p.name, { message: 'Name is required' });
    maxLength(p.name, 100, { message: 'Name cannot exceed 100 characters' });
    required(p.maxStrikes, { message: 'This field is required' });
    min(p.maxStrikes, 3, { message: 'Min value is 3' });
    max(p.maxStrikes, 5000, { message: 'Max value is 5000' });
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
      untracked(() => {
        const next: StallRuleFormModel = r ? {
          name: r.name,
          enabled: r.enabled,
          maxStrikes: r.maxStrikes,
          privacyType: r.privacyType,
          minCompletion: r.minCompletionPercentage,
          maxCompletion: r.maxCompletionPercentage,
          resetOnProgress: r.resetStrikesOnProgress,
          minProgress: r.minimumProgress ?? '',
          deletePrivate: r.deletePrivateTorrentsFromClient,
          changeCategory: r.changeCategory ?? false,
        } : { ...this.defaults };
        this.model.set(next);
        this.openSnapshot.set(JSON.stringify(next));
      });
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
    const dto: CreateStallRuleDto = {
      name: m.name.trim(),
      enabled: m.enabled,
      maxStrikes: m.maxStrikes ?? 3,
      privacyType: m.privacyType,
      minCompletionPercentage: m.minCompletion ?? 0,
      maxCompletionPercentage: m.maxCompletion ?? 100,
      resetStrikesOnProgress: m.resetOnProgress,
      minimumProgress: m.minProgress.trim() || null,
      deletePrivateTorrentsFromClient: changeCategory ? false : m.deletePrivate,
      changeCategory,
    };

    const editing = this.rule();
    const request = editing?.id
      ? this.api.updateStallRule(editing.id, dto)
      : this.api.createStallRule(dto);

    this.saving.set(true);
    request.subscribe({
      next: () => {
        this.toast.success(editing ? 'Stall rule updated' : 'Stall rule created');
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
