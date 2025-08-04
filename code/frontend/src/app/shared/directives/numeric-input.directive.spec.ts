import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Component, DebugElement } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { By } from '@angular/platform-browser';
import { NumericInputDirective } from './numeric-input.directive';

@Component({
  template: `
    <form [formGroup]="testForm">
      <input 
        type="text" 
        formControlName="channelId" 
        numericInput
        data-testid="numeric-input"
      />
    </form>
  `
})
class TestComponent {
  testForm = new FormGroup({
    channelId: new FormControl('')
  });
}

describe('NumericInputDirective', () => {
  let component: TestComponent;
  let fixture: ComponentFixture<TestComponent>;
  let inputElement: HTMLInputElement;
  let inputDebugElement: DebugElement;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      declarations: [TestComponent],
      imports: [ReactiveFormsModule, NumericInputDirective]
    }).compileComponents();

    fixture = TestBed.createComponent(TestComponent);
    component = fixture.componentInstance;
    inputDebugElement = fixture.debugElement.query(By.css('[data-testid="numeric-input"]'));
    inputElement = inputDebugElement.nativeElement;
    fixture.detectChanges();
  });

  it('should create', () => {
    expect(component).toBeTruthy();
  });

  it('should allow numeric input', () => {
    // Simulate typing numbers
    inputElement.value = '123456789';
    inputElement.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(component.testForm.get('channelId')?.value).toBe('123456789');
  });

  it('should remove non-numeric characters', () => {
    // Simulate typing mixed input
    inputElement.value = '123abc456def';
    inputElement.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(component.testForm.get('channelId')?.value).toBe('123456');
    expect(inputElement.value).toBe('123456');
  });

  it('should handle Discord channel ID format', () => {
    // Discord channel IDs are typically 18-19 digits
    const discordChannelId = '123456789012345678';
    inputElement.value = discordChannelId;
    inputElement.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(component.testForm.get('channelId')?.value).toBe(discordChannelId);
  });

  it('should prevent non-numeric keypress', () => {
    const event = new KeyboardEvent('keydown', { keyCode: 65 }); // 'A' key
    spyOn(event, 'preventDefault');
    
    inputElement.dispatchEvent(event);
    
    expect(event.preventDefault).toHaveBeenCalled();
  });

  it('should allow control keys', () => {
    const backspaceEvent = new KeyboardEvent('keydown', { keyCode: 8 }); // Backspace
    spyOn(backspaceEvent, 'preventDefault');
    
    inputElement.dispatchEvent(backspaceEvent);
    
    expect(backspaceEvent.preventDefault).not.toHaveBeenCalled();
  });
});
