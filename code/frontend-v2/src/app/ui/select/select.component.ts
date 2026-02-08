import { Component, ChangeDetectionStrategy, input, model, output, signal, ElementRef, inject, HostListener } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIcon } from '@ng-icons/core';
import { DocumentationService } from '@core/services/documentation.service';

export interface SelectOption {
  label: string;
  value: unknown;
  disabled?: boolean;
}

@Component({
  selector: 'app-select',
  standalone: true,
  imports: [FormsModule, NgIcon],
  templateUrl: './select.component.html',
  styleUrl: './select.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SelectComponent {
  private readonly docs = inject(DocumentationService);

  label = input<string>();
  placeholder = input('Select...');
  options = input<SelectOption[]>([]);
  disabled = input(false);
  error = input<string>();
  hint = input<string>();
  helpKey = input<string>();
  value = model<unknown>(null);

  readonly isOpen = signal(false);

  private readonly el = inject(ElementRef);

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.el.nativeElement.contains(event.target)) {
      this.isOpen.set(false);
    }
  }

  get selectedLabel(): string {
    const option = this.options().find((o) => o.value === this.value());
    return option?.label ?? '';
  }

  toggleDropdown(): void {
    if (!this.disabled()) {
      this.isOpen.update((v) => !v);
    }
  }

  selectOption(option: SelectOption): void {
    if (!option.disabled) {
      this.value.set(option.value);
      this.isOpen.set(false);
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.isOpen.set(false);
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      this.toggleDropdown();
    }
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
