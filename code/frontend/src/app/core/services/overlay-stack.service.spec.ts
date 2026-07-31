import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { OverlayStackService, registerOverlayEffect } from './overlay-stack.service';

describe('OverlayStackService', () => {
  it('reports only the most recently registered overlay as top-most', () => {
    const overlays = TestBed.inject(OverlayStackService);

    const first = overlays.register();
    const second = overlays.register();

    expect(overlays.isTopmost(first)).toBe(false);
    expect(overlays.isTopmost(second)).toBe(true);
  });

  it('promotes the previous overlay when the top-most one closes', () => {
    const overlays = TestBed.inject(OverlayStackService);

    const first = overlays.register();
    const second = overlays.register();
    overlays.unregister(second);

    expect(overlays.isTopmost(first)).toBe(true);
  });

  it('ignores an unknown id', () => {
    const overlays = TestBed.inject(OverlayStackService);
    const first = overlays.register();

    overlays.unregister(9999);

    expect(overlays.isTopmost(first)).toBe(true);
  });

  it('reports nothing as top-most when the stack is empty', () => {
    const overlays = TestBed.inject(OverlayStackService);
    const first = overlays.register();

    overlays.unregister(first);

    expect(overlays.isTopmost(first)).toBe(false);
  });
});

describe('registerOverlayEffect', () => {
  function setup(initiallyOpen: boolean) {
    const isOpen = signal(initiallyOpen);
    const isTopmost = TestBed.runInInjectionContext(() => registerOverlayEffect(isOpen));
    return { isOpen, isTopmost, overlays: TestBed.inject(OverlayStackService) };
  }

  it('registers only once the effect has flushed', () => {
    const { isTopmost } = setup(true);

    expect(isTopmost()).toBe(false);

    TestBed.tick();

    expect(isTopmost()).toBe(true);
  });

  it('unregisters when the overlay closes', () => {
    const { isOpen, isTopmost } = setup(true);
    TestBed.tick();

    isOpen.set(false);
    TestBed.tick();

    expect(isTopmost()).toBe(false);
  });

  it('registers again when the overlay reopens', () => {
    const { isOpen, isTopmost } = setup(true);
    TestBed.tick();
    isOpen.set(false);
    TestBed.tick();

    isOpen.set(true);
    TestBed.tick();

    expect(isTopmost()).toBe(true);
  });

  it('keeps only the last opened overlay top-most', () => {
    const first = setup(true);
    TestBed.tick();
    const second = setup(true);
    TestBed.tick();

    expect(first.isTopmost()).toBe(false);
    expect(second.isTopmost()).toBe(true);
  });
});
