import { Component } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { FeatureBadgeService } from '@core/feature-badges/feature-badge.service';
import { NewBadgeComponent } from './new-badge.component';

@Component({
  imports: [NewBadgeComponent],
  template: `<app-new-badge variant="dot" featureId="x" />`,
})
class HostComponent {}

describe('NewBadgeComponent', () => {
  function setup(isNew: boolean): ComponentFixture<HostComponent> {
    TestBed.configureTestingModule({
      providers: [{ provide: FeatureBadgeService, useValue: { isNew: vi.fn(() => isNew) } }],
    });
    const fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('renders the dot variant with an accessible label when the feature is new', () => {
    const fixture = setup(true);

    const dot: HTMLElement | null = fixture.nativeElement.querySelector('.new-dot');
    expect(dot).not.toBeNull();
    expect(dot!.getAttribute('aria-label')).toBe('New feature');
  });

  it('renders no dot when the feature is not new', () => {
    const fixture = setup(false);

    expect(fixture.nativeElement.querySelector('.new-dot')).toBeNull();
  });
});
