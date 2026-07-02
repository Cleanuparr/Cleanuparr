import { Component, ChangeDetectionStrategy, inject, input, output, signal, DestroyRef } from '@angular/core';
import { CardComponent, ButtonComponent, SpinnerComponent } from '@ui';
import { AccountApi } from '@core/api/account.api';
import { ToastService } from '@core/services/toast.service';
import { ConfirmService } from '@core/services/confirm.service';
import { pollPlexPin } from '@shared/utils/plex-pin-poller';

@Component({
  selector: 'app-plex-integration-card',
  standalone: true,
  imports: [CardComponent, ButtonComponent, SpinnerComponent],
  templateUrl: './plex-integration-card.component.html',
  styleUrl: './plex-integration-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PlexIntegrationCardComponent {
  private readonly api = inject(AccountApi);
  private readonly toast = inject(ToastService);
  private readonly confirmService = inject(ConfirmService);
  private readonly destroyRef = inject(DestroyRef);

  readonly linked = input(false);
  readonly username = input('');
  readonly oidcExclusiveMode = input(false);

  /** Emitted after a successful link/unlink so the parent can reload account info. */
  readonly changed = output<void>();

  readonly plexLinking = signal(false);
  readonly plexUnlinking = signal(false);

  startPlexLink(): void {
    this.plexLinking.set(true);
    this.api.linkPlex().subscribe({
      next: (result) => {
        window.open(result.authUrl, '_blank');
        this.pollPlexLink(result.pinId);
      },
      error: () => {
        this.toast.error('Failed to start Plex linking');
        this.plexLinking.set(false);
      },
    });
  }

  private pollPlexLink(pinId: number): void {
    pollPlexPin({
      verify: () => this.api.verifyPlexLink(pinId),
      onCompleted: () => {
        this.plexLinking.set(false);
        this.toast.success('Plex account linked');
        this.changed.emit();
      },
      onError: () => {
        this.plexLinking.set(false);
        this.toast.error('Plex linking failed');
      },
      onTimeout: () => {
        this.plexLinking.set(false);
        this.toast.error('Plex linking timed out');
      },
      destroyRef: this.destroyRef,
    });
  }

  async confirmUnlinkPlex(): Promise<void> {
    const confirmed = await this.confirmService.confirm({
      title: 'Unlink Plex',
      message: 'This will remove your linked Plex account. You will no longer be able to log in with Plex.',
      confirmLabel: 'Unlink',
      destructive: true,
    });
    if (!confirmed) return;

    this.plexUnlinking.set(true);
    this.api.unlinkPlex().subscribe({
      next: () => {
        this.toast.success('Plex account unlinked');
        this.plexUnlinking.set(false);
        this.changed.emit();
      },
      error: () => {
        this.toast.error('Failed to unlink Plex account');
        this.plexUnlinking.set(false);
      },
    });
  }
}
