import { Component, ChangeDetectionStrategy, input } from '@angular/core';
import { NgIcon } from '@ng-icons/core';

@Component({
  selector: 'app-icon',
  standalone: true,
  imports: [NgIcon],
  templateUrl: './icon.component.html',
  styleUrl: './icon.component.scss',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IconComponent {
  name = input.required<string>();
  size = input<'sm' | 'md' | 'lg' | 'xl'>('md');
}
