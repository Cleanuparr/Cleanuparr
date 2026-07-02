import { Component, ChangeDetectionStrategy, inject, input, signal, computed } from '@angular/core';
import { CardComponent, InputComponent, ButtonComponent, SpinnerComponent } from '@ui';
import { AccountApi } from '@core/api/account.api';
import { ToastService } from '@core/services/toast.service';

@Component({
  selector: 'app-change-password-card',
  standalone: true,
  imports: [CardComponent, InputComponent, ButtonComponent, SpinnerComponent],
  templateUrl: './change-password-card.component.html',
  styleUrl: './change-password-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangePasswordCardComponent {
  private readonly api = inject(AccountApi);
  private readonly toast = inject(ToastService);

  readonly oidcExclusiveMode = input(false);

  readonly currentPassword = signal('');
  readonly newPassword = signal('');
  readonly confirmPassword = signal('');
  readonly changingPassword = signal(false);

  readonly newPasswordStrength = computed(() => {
    const pw = this.newPassword();
    if (!pw) return null;
    if (pw.length < 8) return 'weak';
    const hasUpper = /[A-Z]/.test(pw);
    const hasLower = /[a-z]/.test(pw);
    const hasNumber = /[0-9]/.test(pw);
    const hasSpecial = /[^A-Za-z0-9]/.test(pw);
    const score = [hasUpper, hasLower, hasNumber, hasSpecial].filter(Boolean).length;
    if (pw.length >= 12 && score >= 3) return 'strong';
    if (pw.length >= 8 && score >= 2) return 'medium';
    return 'weak';
  });

  changePassword(): void {
    if (this.newPassword() !== this.confirmPassword()) {
      this.toast.error('Passwords do not match');
      return;
    }
    if (this.newPassword().length < 8) {
      this.toast.error('Password must be at least 8 characters');
      return;
    }

    this.changingPassword.set(true);
    this.api.changePassword({
      currentPassword: this.currentPassword(),
      newPassword: this.newPassword(),
    }).subscribe({
      next: () => {
        this.toast.success('Password changed successfully');
        this.currentPassword.set('');
        this.newPassword.set('');
        this.confirmPassword.set('');
        this.changingPassword.set(false);
      },
      error: () => {
        this.toast.error('Failed to change password');
        this.changingPassword.set(false);
      },
    });
  }
}
