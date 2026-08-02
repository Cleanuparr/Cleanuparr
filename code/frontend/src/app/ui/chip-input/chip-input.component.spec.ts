import { Component, signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ChipInputComponent } from './chip-input.component';

@Component({
  imports: [ChipInputComponent],
  template: `<app-chip-input [value]="tags()" (valueChange)="onChange($event)" />`,
})
class HostComponent {
  readonly tags = signal(['alpha', 'beta']);
  readonly emissions: string[][] = [];

  onChange(tags: string[]): void {
    this.emissions.push(tags);
    this.tags.set(tags);
  }
}

describe('ChipInputComponent', () => {
  function setup(): ComponentFixture<HostComponent> {
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  function chips(fixture: ComponentFixture<HostComponent>): string[] {
    return Array.from(fixture.nativeElement.querySelectorAll('.chip')).map((chip) =>
      (chip as HTMLElement).textContent!.replace('×', '').trim(),
    );
  }

  it('renders the initial value without emitting', () => {
    const fixture = setup();

    expect(chips(fixture)).toEqual(['alpha', 'beta']);
    expect(fixture.componentInstance.emissions).toEqual([]);
  });

  it('adds an item on Enter and removes one on the chip button', () => {
    const fixture = setup();
    const input: HTMLInputElement = fixture.nativeElement.querySelector('.chip-input');

    input.value = 'gamma';
    input.dispatchEvent(new Event('input'));
    fixture.detectChanges();
    input.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter' }));
    fixture.detectChanges();

    expect(chips(fixture)).toEqual(['alpha', 'beta', 'gamma']);

    (fixture.nativeElement.querySelector('.chip__remove') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(chips(fixture)).toEqual(['beta', 'gamma']);
    expect(fixture.componentInstance.emissions).toEqual([
      ['alpha', 'beta', 'gamma'],
      ['beta', 'gamma'],
    ]);
  });
});
