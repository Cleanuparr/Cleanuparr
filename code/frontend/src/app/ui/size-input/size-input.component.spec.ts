import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SizeInputComponent, type SizeUnit } from './size-input.component';

const UNITS: SizeUnit[] = [
  { label: 'MB', value: 'MB' },
  { label: 'GB', value: 'GB' },
];

const SUFFIX_UNITS: SizeUnit[] = [
  { label: 'B', value: 'B' },
  { label: 'GB', value: 'GB' },
];

@Component({
  imports: [SizeInputComponent],
  template: `<app-size-input [units]="units" [value]="size()" (valueChange)="onChange($event)" />`,
})
class HostComponent {
  readonly units = UNITS;
  readonly size = signal('');
  readonly emissions: string[] = [];

  onChange(value: string): void {
    this.emissions.push(value);
    this.size.set(value);
  }
}

describe('SizeInputComponent', () => {
  function setup(value: string, units: SizeUnit[] = UNITS): ComponentFixture<SizeInputComponent> {
    const fixture = TestBed.createComponent(SizeInputComponent);
    fixture.componentRef.setInput('units', units);
    fixture.componentRef.setInput('label', 'Max size');
    fixture.componentRef.setInput('value', value);
    fixture.detectChanges();
    return fixture;
  }

  function setupHost(): ComponentFixture<HostComponent> {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('parses a value with a unit suffix and labels the input it describes', () => {
    const fixture = setup('5MB');

    expect(fixture.componentInstance.numericValue()).toBe(5);
    expect(fixture.componentInstance.selectedUnit()).toBe('MB');

    const label: HTMLLabelElement = fixture.nativeElement.querySelector('.size-label');
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.size-field');

    expect(input.id).toBeTruthy();
    expect(label.getAttribute('for')).toBe(input.id);
  });

  it('prefers the longest unit suffix when one unit is a suffix of another', () => {
    const fixture = setup('5GB', SUFFIX_UNITS);

    expect(fixture.componentInstance.numericValue()).toBe(5);
    expect(fixture.componentInstance.selectedUnit()).toBe('GB');
  });

  it('matches unit suffixes case-insensitively', () => {
    const fixture = setup('12gb');

    expect(fixture.componentInstance.numericValue()).toBe(12);
    expect(fixture.componentInstance.selectedUnit()).toBe('GB');
  });

  it('keeps the selected unit when the value is a bare number', () => {
    const fixture = setup('5GB');

    fixture.componentRef.setInput('value', '7');
    fixture.detectChanges();

    expect(fixture.componentInstance.numericValue()).toBe(7);
    expect(fixture.componentInstance.selectedUnit()).toBe('GB');
  });

  it('yields a null numeric value for an empty string while preserving the unit', () => {
    const fixture = setup('5GB');

    fixture.componentRef.setInput('value', '   ');
    fixture.detectChanges();

    expect(fixture.componentInstance.numericValue()).toBeNull();
    expect(fixture.componentInstance.selectedUnit()).toBe('GB');
  });

  it('yields a null numeric value for unparseable input while preserving the unit', () => {
    const fixture = setup('5GB');

    fixture.componentRef.setInput('value', 'not a size');
    fixture.detectChanges();

    expect(fixture.componentInstance.numericValue()).toBeNull();
    expect(fixture.componentInstance.selectedUnit()).toBe('GB');
  });

  it('emits once per interaction when a number is typed and a unit is picked', () => {
    const fixture = setupHost();
    const host = fixture.componentInstance;
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.size-field');

    input.value = '5';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();

    expect(host.emissions).toEqual(['5MB']);

    const gigabytes = Array.from(
      fixture.nativeElement.querySelectorAll('.size-unit-btn'),
    ).find((button) => (button as HTMLButtonElement).textContent!.trim() === 'GB') as HTMLButtonElement;
    gigabytes.click();
    fixture.detectChanges();

    expect(host.emissions).toEqual(['5MB', '5GB']);

    fixture.detectChanges();

    expect(host.emissions).toEqual(['5MB', '5GB']);
    expect(host.size()).toBe('5GB');
  });
});
