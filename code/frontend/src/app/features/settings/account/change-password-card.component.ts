import { Component, ChangeDetectionStrategy, inject, input, signal, computed } from '@angular/core';
import { form, required, minLength, validate, FormField } from '@angular/forms/signals';
import { CardComponent, InputComponent, ButtonComponent, SpinnerComponent } from '@ui';
import { AccountApi } from '@core/api/account.api';
import { ToastService } from '@core/services/toast.service';

interface ChangePasswordFormModel {
  currentPassword: string;
  newPassword: string;
  confirmPassword: string;
}

@Component({
  selector: 'app-change-password-card',
  standalone: true,
  imports: [CardComponent, InputComponent, ButtonComponent, SpinnerComponent, FormField],
  templateUrl: './change-password-card.component.html',
  styleUrl: './change-password-card.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangePasswordCardComponent {
  private readonly api = inject(AccountApi);
  private readonly toast = inject(ToastService);

  readonly oidcExclusiveMode = input(false);

  private readonly model = signal<ChangePasswordFormModel>({
    currentPassword: '',
    newPassword: '',
    confirmPassword: '',
  });

  readonly passwordForm = form(this.model, (p) => {
    required(p.currentPassword, { message: 'Current password is required' });

    required(p.newPassword, { message: 'New password is required' });
    minLength(p.newPassword, 8, { message: 'Password must be at least 8 characters' });

    required(p.confirmPassword, { message: 'Please confirm the new password' });
    validate(p.confirmPassword, () => {
      const m = this.model();
      return m.confirmPassword && m.newPassword !== m.confirmPassword
        ? { kind: 'mismatch', message: 'Passwords do not match' }
        : undefined;
    });
  });

  readonly changingPassword = signal(false);

  readonly newPasswordStrength = computed(() => {
    const pw = this.model().newPassword;
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
    this.changingPassword.set(true);
    this.api.changePassword({
      currentPassword: this.model().currentPassword,
      newPassword: this.model().newPassword,
    }).subscribe({
      next: () => {
        this.toast.success('Password changed successfully');
        this.model.set({ currentPassword: '', newPassword: '', confirmPassword: '' });
        this.changingPassword.set(false);
      },
      error: () => {
        this.toast.error('Failed to change password');
        this.changingPassword.set(false);
      },
    });
  }
}
