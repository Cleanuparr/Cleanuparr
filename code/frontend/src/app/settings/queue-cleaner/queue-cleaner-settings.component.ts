import { Component, EventEmitter, OnDestroy, Output, effect, inject } from "@angular/core";
import { CommonModule } from "@angular/common";
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from "@angular/forms";
import { Subject, takeUntil } from "rxjs";
import { QueueCleanerConfigStore } from "./queue-cleaner-config.store";
import { CanComponentDeactivate } from "../../core/guards";
import {
  QueueCleanerConfig,
  ScheduleUnit,
  ScheduleOptions
} from "../../shared/models/queue-cleaner-config.model";
import { ByteSizeInputComponent } from "../../shared/components/byte-size-input/byte-size-input.component";
import { MobileAutocompleteComponent } from "../../shared/components/mobile-autocomplete/mobile-autocomplete.component";

// PrimeNG Components
import { CardModule } from "primeng/card";
import { InputTextModule } from "primeng/inputtext";
import { CheckboxModule } from "primeng/checkbox";
import { ButtonModule } from "primeng/button";
import { InputNumberModule } from "primeng/inputnumber";
import { AccordionModule } from "primeng/accordion";
import { SelectButtonModule } from "primeng/selectbutton";
import { ChipsModule } from "primeng/chips";
import { ToastModule } from "primeng/toast";
import { TagModule } from "primeng/tag";
import { MessageService, ConfirmationService } from "primeng/api";
// Using centralized NotificationService instead of MessageService
import { NotificationService } from "../../core/services/notification.service";
import { DocumentationService } from "../../core/services/documentation.service";
import { SelectModule } from "primeng/select";
import { AutoCompleteModule } from "primeng/autocomplete";
import { DropdownModule } from "primeng/dropdown";
import { TooltipModule } from "primeng/tooltip";
import { DialogModule } from "primeng/dialog";
import { ConfirmDialogModule } from "primeng/confirmdialog";
import { LoadingErrorStateComponent } from "../../shared/components/loading-error-state/loading-error-state.component";
import { StallRule, SlowRule, TorrentPrivacyType } from "../../shared/models/queue-rule.model";

// Frontend Coverage Analysis Types
interface CoverageGap {
  start: number;
  end: number;
  privacyType: TorrentPrivacyType;
  privacyTypeLabel: string;
}

interface RuleCoverage {
  hasGaps: boolean;
  gaps: CoverageGap[];
  totalGapPercentage: number;
}

@Component({
  selector: "app-queue-cleaner-settings",
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    CardModule,
    InputTextModule,
    CheckboxModule,
    ButtonModule,
    InputNumberModule,
    AccordionModule,
    SelectButtonModule,
    ChipsModule,
    ToastModule,
  TagModule,
    ByteSizeInputComponent,
    SelectModule,
    AutoCompleteModule,
    DropdownModule,
    TooltipModule,
    DialogModule,
    ConfirmDialogModule,
    LoadingErrorStateComponent,
    MobileAutocompleteComponent,
  ],
  providers: [QueueCleanerConfigStore, MessageService, ConfirmationService],
  templateUrl: "./queue-cleaner-settings.component.html",
  styleUrls: ["./queue-cleaner-settings.component.scss"],
})
export class QueueCleanerSettingsComponent implements OnDestroy, CanComponentDeactivate {
  @Output() saved = new EventEmitter<void>();
  @Output() error = new EventEmitter<string>();

  // Queue Cleaner Configuration Form
  queueCleanerForm: FormGroup;
  
  // Original form values for tracking changes
  private originalFormValues: any;
  
  // Track whether the form has actual changes compared to original values
  hasActualChanges = false;

  // Schedule unit options for job schedules
  scheduleUnitOptions = [
    { label: "Seconds", value: ScheduleUnit.Seconds },
    { label: "Minutes", value: ScheduleUnit.Minutes },
    { label: "Hours", value: ScheduleUnit.Hours },
  ];
  
  // Options for each schedule unit
  scheduleValueOptions = {
    [ScheduleUnit.Seconds]: ScheduleOptions[ScheduleUnit.Seconds].map(v => ({ label: v.toString(), value: v })),
    [ScheduleUnit.Minutes]: ScheduleOptions[ScheduleUnit.Minutes].map(v => ({ label: v.toString(), value: v })),
    [ScheduleUnit.Hours]: ScheduleOptions[ScheduleUnit.Hours].map(v => ({ label: v.toString(), value: v }))
  };
  
  // Display modes for schedule
  scheduleModeOptions = [
    { label: 'Basic', value: false },
    { label: 'Advanced', value: true }
  ];

  // Privacy type options for rules
  privacyTypeOptions = [
    { label: 'Public Torrents Only', value: TorrentPrivacyType.Public },
    { label: 'Private Torrents Only', value: TorrentPrivacyType.Private },
    { label: 'Both Public and Private', value: TorrentPrivacyType.Both }
  ];

  // Inject the necessary services
  private formBuilder = inject(FormBuilder);
  // Using the notification service for all toast messages
  private notificationService = inject(NotificationService);
  private confirmationService = inject(ConfirmationService);
  private documentationService = inject(DocumentationService);
  private queueCleanerStore = inject(QueueCleanerConfigStore);

  // Signals from the store
  readonly queueCleanerConfig = this.queueCleanerStore.config;
  readonly queueCleanerLoading = this.queueCleanerStore.loading;
  readonly queueCleanerSaving = this.queueCleanerStore.saving;
  readonly queueCleanerLoadError = this.queueCleanerStore.loadError;  // Only for "Not connected" state
  readonly queueCleanerSaveError = this.queueCleanerStore.saveError;  // Only for toast notifications

  // Queue Rules signals from the store
  readonly stallRules = this.queueCleanerStore.stallRules;
  readonly slowRules = this.queueCleanerStore.slowRules;
  readonly rulesLoading = this.queueCleanerStore.rulesLoading;
  readonly rulesSaving = this.queueCleanerStore.rulesSaving;
  readonly rulesError = this.queueCleanerStore.rulesError;

  // Track active accordion tabs
  activeAccordionIndices: number[] = [];

  // Modal visibility state
  stallRuleModalVisible = false;
  slowRuleModalVisible = false;

  // Rule forms
  stallRuleForm: FormGroup;
  slowRuleForm: FormGroup;

  // Track saving states locally for UI feedback
  stallRuleSaving = false;
  slowRuleSaving = false;
  
  // Track if we've initiated a save operation to watch for completion
  private stallRuleSaveInitiated = false;
  private slowRuleSaveInitiated = false;

  // Edit mode tracking
  editingStallRule: StallRule | null = null;
  editingSlowRule: SlowRule | null = null;

  // Rule preview properties for summary display (now using signals directly)
  stallRulesPreview: StallRule[] = [];
  slowRulesPreview: SlowRule[] = [];

  // Subject for unsubscribing from observables when component is destroyed
  private destroy$ = new Subject<void>();

  // Computed properties for rule counts and summaries (using signals)
  get stallRulesCount(): number {
    return this.stallRules().length;
  }

  get enabledStallRulesCount(): number {
    return this.stallRules().filter(rule => rule.enabled).length;
  }

  get slowRulesCount(): number {
    return this.slowRules().length;
  }

  get enabledSlowRulesCount(): number {
    return this.slowRules().filter(rule => rule.enabled).length;
  }

  // Coverage analysis computed properties  
  get stallRulesCoverage(): RuleCoverage {
    return this.analyzeRuleCoverage(this.stallRules());
  }

  get slowRulesCoverage(): RuleCoverage {
    return this.analyzeRuleCoverage(this.slowRules());
  }

  /**
   * Analyze rule coverage for gaps in completion percentage intervals
   */
  private analyzeRuleCoverage(rules: (StallRule | SlowRule)[]): RuleCoverage {
    const enabledRules = rules.filter(rule => rule.enabled);
    
    if (enabledRules.length === 0) {
      return {
        hasGaps: true,
        gaps: [
          {
            start: 1,
            end: 100,
            privacyType: TorrentPrivacyType.Public,
            privacyTypeLabel: 'Public'
          },
          {
            start: 1,
            end: 100,
            privacyType: TorrentPrivacyType.Private,
            privacyTypeLabel: 'Private'
          }
        ],
        totalGapPercentage: 100
      };
    }

    // Expand privacy types (Both -> Public + Private) and create intervals
    const intervals: Array<{ start: number; end: number; privacyType: TorrentPrivacyType; privacyTypeLabel: string }> = [];
    
    for (const rule of enabledRules) {
      const start = 1;
      const end = rule.maxCompletionPercentage;
      
      if (rule.privacyType === TorrentPrivacyType.Both) {
        intervals.push({
          start,
          end,
          privacyType: TorrentPrivacyType.Public,
          privacyTypeLabel: 'Public'
        });
        intervals.push({
          start,
          end,
          privacyType: TorrentPrivacyType.Private,
          privacyTypeLabel: 'Private'
        });
      } else {
        intervals.push({
          start,
          end,
          privacyType: rule.privacyType,
          privacyTypeLabel: rule.privacyType === TorrentPrivacyType.Public ? 'Public' : 'Private'
        });
      }
    }

    // Find the maximum coverage for each privacy type
    const gaps: CoverageGap[] = [];
    
    for (const privacyType of [TorrentPrivacyType.Public, TorrentPrivacyType.Private]) {
      const privacyTypeLabel = privacyType === TorrentPrivacyType.Public ? 'Public' : 'Private';
      const typeIntervals = intervals
        .filter(r => r.privacyType === privacyType)
        .sort((a, b) => b.end - a.end); // Sort by end descending to get max coverage

      if (typeIntervals.length === 0) {
        // No rules for this privacy type - entire range is a gap
        gaps.push({
          start: 1,
          end: 100,
          privacyType,
          privacyTypeLabel
        });
        continue;
      }

      // Get the maximum coverage for this privacy type
      const maxCoverage = typeIntervals[0].end;
      
      // If max coverage is less than 100%, there's a gap
      if (maxCoverage < 100) {
        gaps.push({
          start: maxCoverage + 1,
          end: 100,
          privacyType,
          privacyTypeLabel
        });
      }
    }

    // Calculate total gap percentage
    let totalGapPercentage = 0;
    for (const gap of gaps) {
      totalGapPercentage += gap.end - gap.start + 1;
    }
    // Divide by 2 since we have two privacy types (avoid double counting)
    if (gaps.length > 0) {
      totalGapPercentage = totalGapPercentage / 2;
    }

    return {
      hasGaps: gaps.length > 0,
      gaps,
      totalGapPercentage
    };
  }

  /**
   * Check if component can be deactivated (navigation guard)
   */
  canDeactivate(): boolean {
    return !this.queueCleanerForm.dirty;
  }

  /**
   * Open stall rules modal for adding a new rule
   */
  openStallRulesModal(): void {
    this.editingStallRule = null;
    this.stallRuleModalVisible = true;
  }

  /**
   * Open stall rules modal for editing an existing rule
   */
  editStallRule(rule: StallRule): void {
    this.editingStallRule = rule;
    this.stallRuleForm.patchValue({
      name: rule.name,
      enabled: rule.enabled,
      maxStrikes: rule.maxStrikes,
      privacyType: rule.privacyType,
      maxCompletionPercentage: rule.maxCompletionPercentage,
      resetStrikesOnProgress: rule.resetStrikesOnProgress,
      deletePrivateTorrentsFromClient: rule.deletePrivateTorrentsFromClient
    });
    
    // Set the proper enabled/disabled state for deletePrivateTorrentsFromClient
    const deletePrivateControl = this.stallRuleForm.get('deletePrivateTorrentsFromClient');
    if (deletePrivateControl) {
      if (rule.privacyType === TorrentPrivacyType.Private || rule.privacyType === TorrentPrivacyType.Both) {
        deletePrivateControl.enable();
      } else {
        deletePrivateControl.disable();
      }
    }
    
    this.stallRuleModalVisible = true;
  }

  /**
   * Delete a stall rule
   */
  deleteStallRule(rule: StallRule): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to delete the stall rule "${rule.name}"?`,
      header: 'Confirm Deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        if (rule.id) {
          this.queueCleanerStore.deleteStallRule(rule.id);
          this.monitorStallRuleDeletion();
        }
      }
    });
  }

  /**
   * Open slow rules modal for adding a new rule
   */
  openSlowRulesModal(): void {
    this.editingSlowRule = null;
    this.slowRuleModalVisible = true;
  }

  /**
   * Open slow rules modal for editing an existing rule
   */
  editSlowRule(rule: SlowRule): void {
    this.editingSlowRule = rule;
    this.slowRuleForm.patchValue({
      name: rule.name,
      enabled: rule.enabled,
      maxStrikes: rule.maxStrikes,
      privacyType: rule.privacyType,
      maxCompletionPercentage: rule.maxCompletionPercentage,
      resetStrikesOnProgress: rule.resetStrikesOnProgress,
      minSpeed: rule.minSpeed,
      maxTimeHours: rule.maxTimeHours,
      ignoreAboveSize: rule.ignoreAboveSize,
      deletePrivateTorrentsFromClient: rule.deletePrivateTorrentsFromClient
    });
    
    // Set the proper enabled/disabled state for deletePrivateTorrentsFromClient
    const deletePrivateControl = this.slowRuleForm.get('deletePrivateTorrentsFromClient');
    if (deletePrivateControl) {
      if (rule.privacyType === TorrentPrivacyType.Private || rule.privacyType === TorrentPrivacyType.Both) {
        deletePrivateControl.enable();
      } else {
        deletePrivateControl.disable();
      }
    }
    
    this.slowRuleModalVisible = true;
  }

  /**
   * Delete a slow rule
   */
  deleteSlowRule(rule: SlowRule): void {
    this.confirmationService.confirm({
      message: `Are you sure you want to delete the slow rule "${rule.name}"?`,
      header: 'Confirm Deletion',
      icon: 'pi pi-exclamation-triangle',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => {
        if (rule.id) {
          this.queueCleanerStore.deleteSlowRule(rule.id);
          this.monitorSlowRuleDeletion();
        }
      }
    });
  }

  /**
   * Close stall rule modal
   */
  closeStallRuleModal(): void {
    this.stallRuleModalVisible = false;
    this.editingStallRule = null;
    this.stallRuleForm.reset({
      enabled: true,
      maxStrikes: 3,
      privacyType: TorrentPrivacyType.Public,
      resetStrikesOnProgress: true,
      deletePrivateTorrentsFromClient: false,
    });
    
    // Ensure deletePrivateTorrentsFromClient is disabled for Public privacy type
    const deletePrivateControl = this.stallRuleForm.get('deletePrivateTorrentsFromClient');
    if (deletePrivateControl) {
      deletePrivateControl.disable();
    }
  }

  /**
   * Close slow rule modal
   */
  closeSlowRuleModal(): void {
    this.slowRuleModalVisible = false;
    this.editingSlowRule = null;
    this.slowRuleForm.reset({
      enabled: true,
      maxStrikes: 3,
      privacyType: TorrentPrivacyType.Public,
      resetStrikesOnProgress: true,
      maxTimeHours: 0,
      deletePrivateTorrentsFromClient: false,
    });
    
    // Ensure deletePrivateTorrentsFromClient is disabled for Public privacy type
    const deletePrivateControl = this.slowRuleForm.get('deletePrivateTorrentsFromClient');
    if (deletePrivateControl) {
      deletePrivateControl.disable();
    }
  }

  /**
   * Save stall rule (create or update)
   */
  saveStallRule(): void {
    if (this.stallRuleForm.invalid) {
      this.markFormGroupTouched(this.stallRuleForm);
      return;
    }
    
    this.stallRuleSaving = true;
    this.stallRuleSaveInitiated = true;
    const ruleData = this.stallRuleForm.value;
    
    if (this.editingStallRule?.id) {
      // Update existing rule
      this.queueCleanerStore.updateStallRule({ 
        id: this.editingStallRule.id, 
        rule: { ...ruleData, id: this.editingStallRule.id } 
      });
    } else {
      // Create new rule
      this.queueCleanerStore.createStallRule(ruleData);
    }

  }

  /**
   * Save slow rule (create or update)
   */
  saveSlowRule(): void {
    if (this.slowRuleForm.invalid) {
      this.markFormGroupTouched(this.slowRuleForm);
      return;
    }
    
    this.slowRuleSaving = true;
    this.slowRuleSaveInitiated = true;
    const ruleData = this.slowRuleForm.value;
    
    if (this.editingSlowRule?.id) {
      // Update existing rule
      this.queueCleanerStore.updateSlowRule({ 
        id: this.editingSlowRule.id, 
        rule: { ...ruleData, id: this.editingSlowRule.id } 
      });
    } else {
      // Create new rule
      this.queueCleanerStore.createSlowRule(ruleData);
    }

  }

  /**
   * Monitor stall rule deletion completion
   */
  private monitorStallRuleDeletion(): void {
    const checkDeletionStatus = () => {
      const saving = this.rulesSaving();
      const error = this.rulesError();
      
      if (!saving) {
        if (error) {
          this.notificationService.showError(`Deletion failed: ${error}`);
        } else {
          this.notificationService.showSuccess('Stall rule deleted successfully');
        }
      } else {
        setTimeout(checkDeletionStatus, 100);
      }
    };
    
    setTimeout(checkDeletionStatus, 100);
  }

  /**
   * Monitor slow rule deletion completion
   */
  private monitorSlowRuleDeletion(): void {
    const checkDeletionStatus = () => {
      const saving = this.rulesSaving();
      const error = this.rulesError();
      
      if (!saving) {
        if (error) {
          this.notificationService.showError(`Deletion failed: ${error}`);
        } else {
          this.notificationService.showSuccess('Slow rule deleted successfully');
        }
      } else {
        setTimeout(checkDeletionStatus, 100);
      }
    };
    
    setTimeout(checkDeletionStatus, 100);
  }

  /**
   * Track function for stall rules
   */
  trackStallRule(index: number, rule: StallRule): any {
    return rule.id || index;
  }

  /**
   * Track function for slow rules
   */
  trackSlowRule(index: number, rule: SlowRule): any {
    return rule.id || index;
  }

  /**
   * Load rule previews for summary display
   * Since we're now using signals from the store, we don't need to load separately
   */
  private loadRulePreviews(): void {
    // Rules are automatically loaded by the store and available through signals
    // The template will use computed properties to show the preview
  }

  /**
   * Open field-specific documentation in a new tab
   * @param fieldName The form field name (e.g., 'enabled', 'failedImport.maxStrikes')
   */
  openFieldDocs(fieldName: string): void {
    this.documentationService.openFieldDocumentation('queue-cleaner', fieldName);
  }

  constructor() {
    // Initialize the queue cleaner form with proper disabled states
    this.queueCleanerForm = this.formBuilder.group({
      enabled: [false],
      useAdvancedScheduling: [{ value: false, disabled: true }],
      cronExpression: [{ value: '', disabled: true }, [Validators.required]],
      jobSchedule: this.formBuilder.group({
        every: [{ value: 5, disabled: true }, [Validators.required, Validators.min(1)]],
        type: [{ value: ScheduleUnit.Minutes, disabled: true }],
      }),
      ignoredDownloads: [{ value: [], disabled: true }],

      // Failed Import settings - nested group
      failedImport: this.formBuilder.group({
        maxStrikes: [0, [Validators.required, Validators.min(0), Validators.max(5000)]],
        ignorePrivate: [{ value: false, disabled: true }],
        deletePrivate: [{ value: false, disabled: true }],
        ignoredPatterns: [{ value: [], disabled: true }],
      }),

      downloadingMetadataMaxStrikes: [{ value: 0, disabled: true }, [Validators.required, Validators.min(0), Validators.max(5000)]],
    });

    // Initialize rule forms with all required fields like the existing modals
    this.stallRuleForm = this.formBuilder.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      enabled: [true],
      maxStrikes: [3, [Validators.required, Validators.min(3), Validators.max(5000)]],
      privacyType: [TorrentPrivacyType.Public, [Validators.required]],
      maxCompletionPercentage: [null, [Validators.required, Validators.min(1), Validators.max(100)]],
      resetStrikesOnProgress: [true],
      deletePrivateTorrentsFromClient: [{ value: false, disabled: true }],
    });

    this.slowRuleForm = this.formBuilder.group({
      name: ['', [Validators.required, Validators.maxLength(100)]],
      enabled: [true],
      maxStrikes: [3, [Validators.required, Validators.min(3), Validators.max(5000)]],
      minSpeed: ['', [Validators.required]],
      maxTimeHours: [0, [Validators.required, Validators.min(0)]],
      privacyType: [TorrentPrivacyType.Public, [Validators.required]],
      maxCompletionPercentage: [null, [Validators.required, Validators.min(1), Validators.max(100)]],
      ignoreAboveSize: [null, [Validators.min(0)]],
      resetStrikesOnProgress: [true],
      deletePrivateTorrentsFromClient: [{ value: false, disabled: true }],
    });

    // Initialize the control states properly
    this.initializeRuleFormControlStates();

    // Create an effect to update the form when the configuration changes
    // Effect to handle configuration changes
    effect(() => {
      const config = this.queueCleanerConfig();
      if (config) {
        // Handle the case where ignorePrivate is true but deletePrivate is also true
        // This shouldn't happen, but if it does, correct it
        const correctedConfig = { ...config };
        
        // For Queue Cleaner (apply to all sections)
        if (correctedConfig.failedImport?.ignorePrivate && correctedConfig.failedImport?.deletePrivate) {
          correctedConfig.failedImport.deletePrivate = false;
        }
        
        // Reset form with the corrected config values
        this.queueCleanerForm.patchValue({
          enabled: correctedConfig.enabled,
          useAdvancedScheduling: correctedConfig.useAdvancedScheduling || false,
          cronExpression: correctedConfig.cronExpression,
          jobSchedule: correctedConfig.jobSchedule || {
            every: 5,
            type: ScheduleUnit.Minutes
          },
          ignoredDownloads: correctedConfig.ignoredDownloads || [],
          failedImport: correctedConfig.failedImport,
          downloadingMetadataMaxStrikes: correctedConfig.downloadingMetadataMaxStrikes,
        });

        // Then update all other dependent form control states
        this.updateFormControlDisabledStates(correctedConfig);
        
        // Store original values for dirty checking
        this.storeOriginalValues();

        // Mark form as pristine since we've just loaded the data
        this.queueCleanerForm.markAsPristine();
      }
    });
    
    // Effect to handle load errors - emit to LoadingErrorStateComponent for "Not connected" display
    effect(() => {
      const loadErrorMessage = this.queueCleanerLoadError();
      if (loadErrorMessage) {
        // Load errors should be shown as "Not connected to server" in LoadingErrorStateComponent
        this.error.emit(loadErrorMessage);
      }
    });
    
    // Effect to handle save errors - show as toast notifications for user to fix
    effect(() => {
      const saveErrorMessage = this.queueCleanerSaveError();
      if (saveErrorMessage) {
            // Always show save errors as a toast so the user sees the backend message.
            this.notificationService.showError(saveErrorMessage);
      }
    });

    // Effect to handle stall rule save completion
    effect(() => {
      const saving = this.queueCleanerStore.rulesSaving();
      const error = this.queueCleanerStore.rulesError();
      
      if (this.stallRuleSaveInitiated && !saving) {
        const actionVerb = this.editingStallRule ? 'update' : 'create';

        if (error) {
          this.notificationService.showError(`Failed to ${actionVerb} stall rule: ${error}`);
        } else {
          this.notificationService.showSuccess(`Stall rule ${actionVerb}d successfully`);
          this.closeStallRuleModal();
        }
        this.stallRuleSaving = false;
        this.stallRuleSaveInitiated = false;
        this.queueCleanerStore.resetRulesError();
      }
    });

    // Effect to handle slow rule save completion  
    effect(() => {
      const saving = this.queueCleanerStore.rulesSaving();
      const error = this.queueCleanerStore.rulesError();
      
      if (this.slowRuleSaveInitiated && !saving) {
        const actionVerb = this.editingSlowRule ? 'update' : 'create';

        if (error) {
          this.notificationService.showError(`Failed to ${actionVerb} slow rule: ${error}`);
        } else {
          this.notificationService.showSuccess(`Slow rule ${actionVerb}d successfully`);
          this.closeSlowRuleModal();
        }
        this.slowRuleSaving = false;
        this.slowRuleSaveInitiated = false;
        this.queueCleanerStore.resetRulesError();
      }
    });
    
    // Set up listeners for form value changes
    this.setupFormValueChangeListeners();
  }

  /**
   * Clean up subscriptions when component is destroyed
   */
  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  /**
   * Initialize rule form control states based on initial values
   */
  private initializeRuleFormControlStates(): void {
    // Initialize stall rule form
    const stallPrivacyType = this.stallRuleForm.get('privacyType')?.value;
    const stallDeletePrivateControl = this.stallRuleForm.get('deletePrivateTorrentsFromClient');
    if (stallDeletePrivateControl) {
      if (stallPrivacyType === TorrentPrivacyType.Private || stallPrivacyType === TorrentPrivacyType.Both) {
        stallDeletePrivateControl.enable();
      } else {
        stallDeletePrivateControl.disable();
      }
    }

    // Initialize slow rule form
    const slowPrivacyType = this.slowRuleForm.get('privacyType')?.value;
    const slowDeletePrivateControl = this.slowRuleForm.get('deletePrivateTorrentsFromClient');
    if (slowDeletePrivateControl) {
      if (slowPrivacyType === TorrentPrivacyType.Private || slowPrivacyType === TorrentPrivacyType.Both) {
        slowDeletePrivateControl.enable();
      } else {
        slowDeletePrivateControl.disable();
      }
    }
  }

  /**
   * Set up listeners for form control value changes to manage dependent control states
   */
  private setupFormValueChangeListeners(): void {
    // Listen for changes to the 'enabled' control
    const enabledControl = this.queueCleanerForm.get('enabled');
    if (enabledControl) {
      enabledControl.valueChanges.pipe(takeUntil(this.destroy$))
        .subscribe((enabled: boolean) => {
          this.updateMainControlsState(enabled);
        });
    }

    // Add listeners for ignorePrivate changes in each section
    ['failedImport', 'stalled', 'slow'].forEach(section => {
      const ignorePrivateControl = this.queueCleanerForm.get(`${section}.ignorePrivate`);
      
      if (ignorePrivateControl) {
        ignorePrivateControl.valueChanges.pipe(takeUntil(this.destroy$))
          .subscribe((ignorePrivate: boolean) => {
            const deletePrivateControl = this.queueCleanerForm.get(`${section}.deletePrivate`);
            
            if (ignorePrivate && deletePrivateControl) {
              // If ignoring private, uncheck and disable delete private
              deletePrivateControl.setValue(false);
              deletePrivateControl.disable({ onlySelf: true });
            } else if (!ignorePrivate && deletePrivateControl) {
              // If not ignoring private, enable delete private (if parent section is enabled)
              const sectionEnabled = this.isSectionEnabled(section);
              if (sectionEnabled) {
                deletePrivateControl.enable({ onlySelf: true });
              }
            }
          });
      }
    });
      
    // Listen for changes to the 'useAdvancedScheduling' control
    const advancedControl = this.queueCleanerForm.get('useAdvancedScheduling');
    if (advancedControl) {
      advancedControl.valueChanges.pipe(takeUntil(this.destroy$))
        .subscribe((useAdvanced: boolean) => {
          const enabled = this.queueCleanerForm.get('enabled')?.value || false;
          const cronExpressionControl = this.queueCleanerForm.get('cronExpression');
          const jobScheduleGroup = this.queueCleanerForm.get('jobSchedule') as FormGroup;
          const everyControl = jobScheduleGroup?.get('every');
          const typeControl = jobScheduleGroup?.get('type');
          
          // Update scheduling controls based on mode, regardless of enabled state
          if (useAdvanced) {
            if (cronExpressionControl) cronExpressionControl.enable();
            if (everyControl) everyControl.disable();
            if (typeControl) typeControl.disable();
          } else {
            if (cronExpressionControl) cronExpressionControl.disable();
            if (everyControl) everyControl.enable();
            if (typeControl) typeControl.enable();
          }
          
          // Then respect the main enabled state - if disabled, disable all scheduling controls
          if (!enabled) {
            cronExpressionControl?.disable();
            everyControl?.disable();
            typeControl?.disable();
          }
        });
    }

    // Failed import settings
    const failedImportMaxStrikesControl = this.queueCleanerForm.get("failedImport.maxStrikes");
    if (failedImportMaxStrikesControl) {
      failedImportMaxStrikesControl.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe((strikes) => {
        this.updateFailedImportDependentControls(strikes);
      });
    }

    // Listen for changes to the schedule type to ensure dropdown isn't empty
    const scheduleTypeControl = this.queueCleanerForm.get('jobSchedule.type');
    if (scheduleTypeControl) {
      scheduleTypeControl.valueChanges
        .pipe(takeUntil(this.destroy$))
        .subscribe(() => {
          // Ensure the selected value is valid for the new type
          const everyControl = this.queueCleanerForm.get('jobSchedule.every');
          const currentValue = everyControl?.value;
          const scheduleType = this.queueCleanerForm.get('jobSchedule.type')?.value;
          
          const validValues = ScheduleOptions[scheduleType as keyof typeof ScheduleOptions];
          if (validValues && currentValue && !validValues.includes(currentValue)) {
            everyControl?.setValue(validValues[0]);
          }
        });
    }
      
    // Listen to all form changes to check for actual differences from original values
    this.queueCleanerForm.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe(() => {
        this.hasActualChanges = this.formValuesChanged();
      });

    // Set up privacy type change listeners for rule forms
    this.stallRuleForm.get('privacyType')?.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe((privacyType: TorrentPrivacyType) => {
        const deletePrivateControl = this.stallRuleForm.get('deletePrivateTorrentsFromClient');
        if (deletePrivateControl) {
          // Always reset to false on any privacy type change
          deletePrivateControl.setValue(false);
          
          // Enable/disable based on privacy type
          if (privacyType === TorrentPrivacyType.Private || privacyType === TorrentPrivacyType.Both) {
            deletePrivateControl.enable();
          } else {
            deletePrivateControl.disable();
          }
        }
      });

    this.slowRuleForm.get('privacyType')?.valueChanges.pipe(takeUntil(this.destroy$))
      .subscribe((privacyType: TorrentPrivacyType) => {
        const deletePrivateControl = this.slowRuleForm.get('deletePrivateTorrentsFromClient');
        if (deletePrivateControl) {
          // Always reset to false on any privacy type change
          deletePrivateControl.setValue(false);
          
          // Enable/disable based on privacy type
          if (privacyType === TorrentPrivacyType.Private || privacyType === TorrentPrivacyType.Both) {
            deletePrivateControl.enable();
          } else {
            deletePrivateControl.disable();
          }
        }
      });
  }

  /**
   * Store original form values for dirty checking
   */
  private storeOriginalValues(): void {
    // Create a deep copy of the form values to ensure proper comparison
    this.originalFormValues = JSON.parse(JSON.stringify(this.queueCleanerForm.getRawValue()));
    this.hasActualChanges = false;
  }

  // Helper method to check if a section is enabled
  private isSectionEnabled(section: string): boolean {
    const mainEnabled = this.queueCleanerForm.get('enabled')?.value || false;
    if (!mainEnabled) return false;
    
    const maxStrikesControl = this.queueCleanerForm.get(`${section}.maxStrikes`);
    const maxStrikes = maxStrikesControl?.value || 0;
    
    return maxStrikes >= 3;
  }
  
  // Check if the current form values are different from the original values
  private formValuesChanged(): boolean {
    if (!this.originalFormValues) return false;
    
    const currentValues = this.queueCleanerForm.getRawValue();
    return !this.isEqual(currentValues, this.originalFormValues);
  }
  
  // Deep compare two objects for equality
  private isEqual(obj1: any, obj2: any): boolean {
    if (obj1 === obj2) return true;
    
    if (typeof obj1 !== 'object' || obj1 === null ||
        typeof obj2 !== 'object' || obj2 === null) {
      return obj1 === obj2;
    }
    
    const keys1 = Object.keys(obj1);
    const keys2 = Object.keys(obj2);
    
    if (keys1.length !== keys2.length) return false;
    
    for (const key of keys1) {
      if (!keys2.includes(key)) return false;
      
      if (!this.isEqual(obj1[key], obj2[key])) return false;
    }
    
    return true;
  }

  /**
   * Update form control disabled states based on the configuration
   */
  private updateFormControlDisabledStates(config: QueueCleanerConfig): void {
    // Update main form controls based on the 'enabled' state
    this.updateMainControlsState(config.enabled);

    // Check if failed import strikes are set and update dependent controls
    if (config.failedImport?.maxStrikes !== undefined) {
      this.updateFailedImportDependentControls(config.failedImport.maxStrikes);
    }
  }

  /**
   * Update the state of main controls based on the 'enabled' control value
   */
  private updateMainControlsState(enabled: boolean): void {
    const useAdvancedScheduling = this.queueCleanerForm.get('useAdvancedScheduling')?.value || false;
    const cronExpressionControl = this.queueCleanerForm.get('cronExpression');
    const jobScheduleGroup = this.queueCleanerForm.get('jobSchedule') as FormGroup;
    const everyControl = jobScheduleGroup.get('every');
    const typeControl = jobScheduleGroup.get('type');
    const downloadingMetadataMaxStrikesControl = this.queueCleanerForm.get('downloadingMetadataMaxStrikes');

    if (enabled) {
      // Enable scheduling controls based on mode
      if (useAdvancedScheduling) {
        cronExpressionControl?.enable();
        everyControl?.disable();
        typeControl?.disable();
      } else {
        cronExpressionControl?.disable();
        everyControl?.enable();
        typeControl?.enable();
      }

      // Enable downloading metadata max strikes control
      downloadingMetadataMaxStrikesControl?.enable();
      
      // Enable the useAdvancedScheduling control
      const useAdvancedSchedulingControl = this.queueCleanerForm.get('useAdvancedScheduling');
      useAdvancedSchedulingControl?.enable();
      
      // Enable ignored downloads control
      const ignoredDownloadsControl = this.queueCleanerForm.get('ignoredDownloads');
      ignoredDownloadsControl?.enable();
      
      // Update individual config sections only if they are enabled
      const failedImportMaxStrikes = this.queueCleanerForm.get("failedImport.maxStrikes")?.value;
      const stalledMaxStrikes = this.queueCleanerForm.get("stalled.maxStrikes")?.value;
      const slowMaxStrikes = this.queueCleanerForm.get("slow.maxStrikes")?.value;
      
      this.updateFailedImportDependentControls(failedImportMaxStrikes);
    } else {
      // Disable all scheduling controls
      cronExpressionControl?.disable();
      everyControl?.disable();
      typeControl?.disable();

      // Disable downloading metadata max strikes control
      downloadingMetadataMaxStrikesControl?.disable();
      
      // Disable the useAdvancedScheduling control
      const useAdvancedSchedulingControl = this.queueCleanerForm.get('useAdvancedScheduling');
      useAdvancedSchedulingControl?.disable();
      
      // Disable ignored downloads control
      const ignoredDownloadsControl = this.queueCleanerForm.get('ignoredDownloads');
      ignoredDownloadsControl?.disable();
      
      // Save current active accordion state before clearing it
      // This will be empty when we collapse all accordions
      this.activeAccordionIndices = [];
    }
  }

  /**
   * Update the state of Failed Import dependent controls based on the 'maxStrikes' value
   */
  private updateFailedImportDependentControls(strikes: number): void {
    const enable = strikes >= 3;
    const options = { onlySelf: true };

    if (enable) {
      this.queueCleanerForm.get("failedImport")?.get("ignorePrivate")?.enable(options);
      this.queueCleanerForm.get("failedImport")?.get("ignoredPatterns")?.enable(options);
      
      // Only enable deletePrivate if ignorePrivate is false
      const ignorePrivate = this.queueCleanerForm.get("failedImport.ignorePrivate")?.value || false;
      const deletePrivateControl = this.queueCleanerForm.get("failedImport.deletePrivate");
      
      if (!ignorePrivate && deletePrivateControl) {
        deletePrivateControl.enable(options);
      } else if (deletePrivateControl) {
        deletePrivateControl.disable(options);
      }
    } else {
      this.queueCleanerForm.get("failedImport")?.get("ignorePrivate")?.disable(options);
      this.queueCleanerForm.get("failedImport")?.get("deletePrivate")?.disable(options);
      this.queueCleanerForm.get("failedImport")?.get("ignoredPatterns")?.disable(options);
    }
  }

  /**
   * Save the queue cleaner configuration
   */
  saveQueueCleanerConfig(): void {
    // Mark all form controls as touched to trigger validation messages
    this.markFormGroupTouched(this.queueCleanerForm);
    
    if (this.queueCleanerForm.valid) {
      // Make a copy of the form values
      const formValue = this.queueCleanerForm.getRawValue();
      
      // Determine the correct cron expression to use
      const cronExpression: string = formValue.useAdvancedScheduling ? 
        formValue.cronExpression : 
        // If in basic mode, generate cron expression from the schedule
        this.queueCleanerStore.generateCronExpression(formValue.jobSchedule);
      
      // Create the config object to be saved
      const queueCleanerConfig: QueueCleanerConfig = {
        enabled: formValue.enabled,
        useAdvancedScheduling: formValue.useAdvancedScheduling,
        cronExpression: cronExpression,
        jobSchedule: formValue.jobSchedule,
        ignoredDownloads: formValue.ignoredDownloads || [],
        failedImport: {
          maxStrikes: formValue.failedImport?.maxStrikes || 0,
          ignorePrivate: formValue.failedImport?.ignorePrivate || false,
          deletePrivate: formValue.failedImport?.deletePrivate || false,
          ignoredPatterns: formValue.failedImport?.ignoredPatterns || [],
        },
        downloadingMetadataMaxStrikes: formValue.downloadingMetadataMaxStrikes || 0,
        stallRules: formValue.stallRules || [],
        slowRules: formValue.slowRules || [],
      };
      
      // Save the configuration
      this.queueCleanerStore.saveConfig(queueCleanerConfig);
      
      // Setup a one-time check to mark form as pristine after successful save
      // This pattern works with signals since we're not trying to pipe the signal itself
      const checkSaveCompletion = () => {
        const saving = this.queueCleanerSaving();
        const saveError = this.queueCleanerSaveError();
        
        if (!saving && !saveError) {
          // Mark form as pristine after successful save
          this.queueCleanerForm.markAsPristine();
          // Update original values reference
          this.storeOriginalValues();
          // Emit saved event 
          this.saved.emit();
          // Display success message
          this.notificationService.showSuccess('Queue cleaner configuration saved successfully.');
        } else if (!saving && saveError) {
          // If there's a save error, we can stop checking
          // Toast notification is already handled by the effect above
        } else {
          // If still saving, check again in a moment
          setTimeout(checkSaveCompletion, 100);
        }
      };
      
      // Start checking for save completion
      checkSaveCompletion();
    } else {
      // Form is invalid, show error message
      this.notificationService.showValidationError();
      
      // Emit error for parent components
      this.error.emit("Please fix validation errors before saving.");
    }
  }


  
  /**
   * Reset the queue cleaner configuration form to default values
   */
  resetQueueCleanerConfig(): void {  
    this.queueCleanerForm.reset({
      enabled: false,
      useAdvancedScheduling: false,
      cronExpression: "0 0/5 * * * ?",
      jobSchedule: {
        every: 5,
        type: ScheduleUnit.Minutes,
      },

      // Failed Import settings (nested)
      failedImport: {
        maxStrikes: 0,
        ignorePrivate: false,
        deletePrivate: false,
        ignoredPatterns: [],
      },

      downloadingMetadataMaxStrikes: 0,
    });

    // Manually update control states after reset
    this.updateMainControlsState(false);
    this.updateFailedImportDependentControls(0);
    
    // Mark form as dirty so the form can be saved
    this.queueCleanerForm.markAsDirty();
  }

  /**
   * Get schedule value options based on the selected schedule type
   */
  getScheduleValueOptions() {
    const scheduleType = this.queueCleanerForm.get('jobSchedule.type')?.value;
    return this.scheduleValueOptions[scheduleType as keyof typeof this.scheduleValueOptions] || [];
  }

  /**
   * Check if a nested form field has a specific error (for queueCleanerForm)
   */
  hasNestedError(groupName: string, fieldName: string, errorType: string): boolean {
    const field = this.queueCleanerForm.get(`${groupName}.${fieldName}`);
    return !!(field && field.hasError(errorType) && (field.dirty || field.touched));
  }

  /**
   * Check if a top-level form field has a specific error (for queueCleanerForm)
   */
  hasMainFormError(fieldName: string, errorType: string): boolean {
    const field = this.queueCleanerForm.get(fieldName);
    return !!(field && field.hasError(errorType) && (field.dirty || field.touched));
  }

  /**
   * Check if a modal form field has a specific error (for stallRuleForm and slowRuleForm)
   */
  hasModalError(form: FormGroup, fieldName: string, errorType: string): boolean {
    const field = form.get(fieldName);
    return !!(field && field.hasError(errorType) && (field.dirty || field.touched));
  }

  /**
   * Check if the deletePrivateTorrentsFromClient field should be enabled based on privacy type
   */
  isDeletePrivateTorrentsEnabled(form: FormGroup): boolean {
    const privacyType = form.get('privacyType')?.value;
    return privacyType === TorrentPrivacyType.Private || privacyType === TorrentPrivacyType.Both;
  }

  /**
   * Mark all form controls as touched to trigger validation
   */
  private markFormGroupTouched(formGroup: FormGroup): void {
    Object.keys(formGroup.controls).forEach(key => {
      const control = formGroup.get(key);
      control?.markAsTouched();

      if (control instanceof FormGroup) {
        this.markFormGroupTouched(control);
      }
    });
  }

}
