import { Component } from '@angular/core';
import { ButtonComponent, InputComponent } from '@ui';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ButtonComponent, InputComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {}
