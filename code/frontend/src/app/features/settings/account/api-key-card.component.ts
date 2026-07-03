import { Component, ChangeDetectionStrategy, inject, input, signal } from '@angular/core';
import { CardComponent, ButtonComponent, SpinnerComponent } from '@ui';
import { AccountApi } from '@core/api/account.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';

@Component({
  selector: 'app-api-key-card',
  standalone: true,
  imports: [CardComponent, ButtonComponent, SpinnerComponent],
  templateUrl: './api-key-card.component.html',
  styleUrl: './api-key-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ApiKeyCardComponent {
  private readonly api = inject(AccountApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);

  readonly apiKeyPreview = input('');

  readonly apiKey = signal('');
  readonly apiKeyRevealed = signal(false);
  readonly regeneratingApiKey = signal(false);

  revealApiKey(): void {
    if (this.apiKeyRevealed()) {
      this.apiKeyRevealed.set(false);
      this.apiKey.set('');
      return;
    }

    this.api.getApiKey().subscribe({
      next: (result) => {
        this.apiKey.set(result.apiKey);
        this.apiKeyRevealed.set(true);
      },
      error: () => this.toast.error('Failed to load API key'),
    });
  }

  copyApiKey(): void {
    navigator.clipboard.writeText(this.apiKey()).then(
      () => this.toast.success('API key copied to clipboard'),
      () => this.toast.error('Failed to copy API key'),
    );
  }

  async confirmRegenerateApiKey(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Regenerate API Key',
      message: 'This will invalidate the current API key. Any integrations using this key will stop working.',
      confirmLabel: 'Regenerate',
      destructive: true,
    });
    if (!confirmed) return;

    this.regeneratingApiKey.set(true);
    this.api.regenerateApiKey().subscribe({
      next: (result) => {
        this.apiKey.set(result.apiKey);
        this.apiKeyRevealed.set(true);
        this.toast.success('API key regenerated');
        this.regeneratingApiKey.set(false);
      },
      error: () => {
        this.toast.error('Failed to regenerate API key');
        this.regeneratingApiKey.set(false);
      },
    });
  }
}
