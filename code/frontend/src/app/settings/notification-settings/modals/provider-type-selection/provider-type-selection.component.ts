import { Component, Input, Output, EventEmitter } from '@angular/core';
import { CommonModule } from '@angular/common';
import { DialogModule } from 'primeng/dialog';
import { ButtonModule } from 'primeng/button';
import { NotificationProviderType } from '../../../../shared/models/enums';
import { ProviderTypeInfo } from '../../models/provider-modal.model';

@Component({
  selector: 'app-provider-type-selection',
  standalone: true,
  imports: [
    CommonModule,
    DialogModule,
    ButtonModule
  ],
  template: `
    <p-dialog 
      [(visible)]="visible" 
      [modal]="true" 
      [closable]="true"
      [draggable]="false"
      [resizable]="false"
      styleClass="instance-modal provider-selection-modal"
      header="Add Notification Provider"
      (onHide)="onCancel()">
      
      <div class="provider-selection-content">
        <p class="selection-description">
          Choose a notification provider type to configure:
        </p>
        
        <div class="provider-selection-grid">
          <div 
            class="provider-card" 
            *ngFor="let provider of availableProviders" 
            (click)="selectProvider(provider.type)"
            [attr.data-provider]="provider.type">
            
            <div class="provider-icon">
              <i [class]="provider.iconClass"></i>
            </div>
            <div class="provider-name">
              {{ provider.name }}
            </div>
            <div class="provider-description" *ngIf="provider.description">
              {{ provider.description }}
            </div>
          </div>
        </div>
      </div>

      <ng-template pTemplate="footer">
        <div class="modal-footer">
          <button 
            pButton 
            type="button" 
            label="Cancel" 
            class="p-button-text"
            (click)="onCancel()">
          </button>
        </div>
      </ng-template>
    </p-dialog>
  `,
  styleUrls: ['./provider-type-selection.component.scss']
})
export class ProviderTypeSelectionComponent {
  @Input() visible = false;
  @Output() providerSelected = new EventEmitter<NotificationProviderType>();
  @Output() cancel = new EventEmitter<void>();

  // Available providers - only show implemented ones
  availableProviders: ProviderTypeInfo[] = [
    {
      type: NotificationProviderType.Notifiarr,
      name: 'Notifiarr',
      iconClass: 'pi pi-bell',
      description: 'Discord integration via Notifiarr service'
    },
    {
      type: NotificationProviderType.Apprise,
      name: 'Apprise',
      iconClass: 'pi pi-send',
      description: 'Universal notification library supporting many services'
    }
    // Add more providers as they are implemented
  ];

  selectProvider(type: NotificationProviderType) {
    this.providerSelected.emit(type);
  }

  onCancel() {
    this.cancel.emit();
  }
}
