import { Component, ChangeDetectionStrategy, input, model, output, computed, inject } from '@angular/core';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';
import { NewBadgeComponent } from '@ui/new-badge/new-badge.component';
import { SelectComponent, SelectOption } from '@ui/select/select.component';
import { generateControlId } from '@ui/control-id';

function padded(count: number): SelectOption[] {
  return Array.from({ length: count }, (_, i) => {
    const text = String(i).padStart(2, '0');
    return { label: text, value: text };
  });
}

const HOURS = padded(24);
const MINUTES = padded(60);

@Component({
  selector: 'app-time-input',
  standalone: true,
  imports: [NgIcon, NewBadgeComponent, SelectComponent],
  templateUrl: './time-input.component.html',
  styleUrl: './time-input.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TimeInputComponent {
  private readonly docs = inject(DocumentationService);

  protected readonly controlId = generateControlId('app-time');

  label = input<string>();
  featureId = input<string>();
  disabled = input(false);
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  value = model('');
  touch = output<void>();

  protected readonly hours = HOURS;
  protected readonly minutes = MINUTES;

  protected readonly hour = computed(() => (this.value() ? this.value().slice(0, 2) : null));
  protected readonly minute = computed(() => (this.value() ? this.value().slice(3, 5) : null));

  setHour(hour: unknown): void {
    this.value.set(`${hour}:${this.minute() ?? '00'}`);
    this.touch.emit();
  }

  setMinute(minute: unknown): void {
    this.value.set(`${this.hour() ?? '00'}:${minute}`);
    this.touch.emit();
  }

  onHelpClick(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const key = this.helpKey();
    if (key) {
      const [section, field] = key.split(':');
      this.docs.openFieldDocumentation(section, field);
    }
  }
}
