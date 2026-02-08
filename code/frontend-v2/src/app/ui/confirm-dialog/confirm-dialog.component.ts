import { Component, ChangeDetectionStrategy, inject, HostListener } from '@angular/core';
import { ConfirmService } from '@core/services/confirm.service';
import { ButtonComponent } from '../button/button.component';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [ButtonComponent],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ConfirmDialogComponent {
  readonly confirm = inject(ConfirmService);

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.confirm.state()) {
      this.confirm.cancel();
    }
  }
}
