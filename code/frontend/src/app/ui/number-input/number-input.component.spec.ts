import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { NumberInputComponent } from './number-input.component';

@Component({
  imports: [NumberInputComponent],
  template: `<app-number-input
    [value]="value()"
    [min]="min()"
    [max]="max()"
    [step]="step()"
    (valueChange)="onChange($event)"
  />`,
})
class HostComponent {
  readonly value = signal<number | null>(null);
  readonly min = signal<number | undefined>(undefined);
  readonly max = signal<number | undefined>(undefined);
  readonly step = signal(1);
  readonly emissions: (number | null)[] = [];

  onChange(value: number | null): void {
    this.emissions.push(value);
    this.value.set(value);
  }
}

describe('NumberInputComponent', () => {
  function setup(): ComponentFixture<HostComponent> {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  function field(fixture: ComponentFixture<HostComponent>): HTMLInputElement {
    return fixture.nativeElement.querySelector('.number-field');
  }

  function button(fixture: ComponentFixture<HostComponent>, label: string): HTMLButtonElement {
    return fixture.nativeElement.querySelector(`.number-btn[aria-label="${label}"]`);
  }

  it('clamps a below-minimum value up and an above-maximum value down on blur', () => {
    const fixture = setup();
    const host = fixture.componentInstance;
    host.min.set(5);
    host.max.set(50);
    host.value.set(1);
    fixture.detectChanges();

    field(fixture).dispatchEvent(new FocusEvent('blur'));
    fixture.detectChanges();

    expect(host.value()).toBe(5);

    host.value.set(100);
    fixture.detectChanges();
    field(fixture).dispatchEvent(new FocusEvent('blur'));
    fixture.detectChanges();

    expect(host.value()).toBe(50);
    expect(host.emissions).toEqual([5, 50]);
  });

  it('does not emit when blurring a value that is already within range', () => {
    const fixture = setup();
    const host = fixture.componentInstance;
    host.min.set(5);
    host.max.set(50);
    host.value.set(10);
    fixture.detectChanges();

    field(fixture).dispatchEvent(new FocusEvent('blur'));
    fixture.detectChanges();

    expect(host.emissions).toEqual([]);
    expect(host.value()).toBe(10);
  });

  it('yields null for an emptied input and the parsed number for typed digits', () => {
    const fixture = setup();
    const host = fixture.componentInstance;
    host.value.set(7);
    fixture.detectChanges();

    const input = field(fixture);
    input.value = '';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(host.value()).toBeNull();

    input.value = '42';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(host.value()).toBe(42);
    expect(host.emissions).toEqual([null, 42]);
  });

  it('increments by a fractional step without floating point drift', () => {
    const fixture = setup();
    const host = fixture.componentInstance;
    host.step.set(0.1);
    host.value.set(0.2);
    fixture.detectChanges();

    button(fixture, 'Increase').click();
    fixture.detectChanges();

    expect(host.value()).toBe(0.3);
    expect(host.emissions).toEqual([0.3]);
  });

  it('holds increment and decrement inside the min and max bounds', () => {
    const fixture = setup();
    const host = fixture.componentInstance;
    host.min.set(0);
    host.max.set(3);
    host.value.set(3);
    fixture.detectChanges();

    button(fixture, 'Increase').click();
    fixture.detectChanges();

    expect(host.value()).toBe(3);
    expect(host.emissions).toEqual([]);

    host.value.set(0);
    fixture.detectChanges();
    button(fixture, 'Decrease').click();
    fixture.detectChanges();

    expect(host.value()).toBe(0);
    expect(host.emissions).toEqual([]);
  });

  it('ignores increment while disabled', () => {
    const fixture = TestBed.createComponent(NumberInputComponent);
    fixture.componentRef.setInput('value', 5);
    fixture.componentRef.setInput('disabled', true);
    fixture.detectChanges();

    fixture.componentInstance.increment();
    fixture.detectChanges();

    expect(fixture.componentInstance.value()).toBe(5);
  });
});
