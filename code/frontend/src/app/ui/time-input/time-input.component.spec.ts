import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { TimeInputComponent } from './time-input.component';

@Component({
  imports: [TimeInputComponent],
  template: `<app-time-input [value]="time()" (valueChange)="onChange($event)" />`,
})
class HostComponent {
  readonly time = signal('07:30');
  readonly emissions: string[] = [];

  onChange(value: string): void {
    this.emissions.push(value);
    this.time.set(value);
  }
}

describe('TimeInputComponent', () => {
  function setup(initial = '07:30'): ComponentFixture<HostComponent> {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.componentInstance.time.set(initial);
    fixture.detectChanges();
    return fixture;
  }

  function triggers(fixture: ComponentFixture<HostComponent>): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.select-trigger__text')).map((el) =>
      (el as HTMLElement).textContent!.trim(),
    );
  }

  function open(fixture: ComponentFixture<HostComponent>, index: number): void {
    (fixture.nativeElement.querySelectorAll('.select-trigger')[index] as HTMLButtonElement).click();
    fixture.detectChanges();
  }

  function options(fixture: ComponentFixture<HostComponent>): HTMLButtonElement[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.select-option'));
  }

  it('renders the hour and minute of the value', () => {
    expect(triggers(setup('07:30'))).toEqual(['07', '30']);
  });

  it('shows placeholders when the value is empty', () => {
    expect(triggers(setup(''))).toEqual(['HH', 'MM']);
  });

  it('keeps the minute when an hour is picked', () => {
    const fixture = setup('07:30');

    open(fixture, 0);
    options(fixture).find((o) => o.textContent!.trim() === '23')!.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.emissions).toEqual(['23:30']);
  });

  it('defaults the hour to 00 when a minute is picked from empty', () => {
    const fixture = setup('');

    open(fixture, 1);
    options(fixture).find((o) => o.textContent!.trim() === '45')!.click();
    fixture.detectChanges();

    expect(fixture.componentInstance.emissions).toEqual(['00:45']);
  });

  it('offers 24 hours from 00 to 23 and 60 minutes without AM or PM', () => {
    const fixture = setup('07:30');

    open(fixture, 0);
    const hours = options(fixture).map((o) => o.textContent!.trim());

    expect(hours.length).toBe(24);
    expect(hours[0]).toBe('00');
    expect(hours[23]).toBe('23');
    expect(hours.some((h) => /AM|PM/i.test(h))).toBe(false);

    open(fixture, 1);
    expect(options(fixture).length).toBe(60);
  });
});
