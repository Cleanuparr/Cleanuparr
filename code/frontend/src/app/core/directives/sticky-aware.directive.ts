import { Directive, ElementRef, OnDestroy, OnInit, inject } from '@angular/core';

/**
 * Toggles `.is-stuck` on the host element when it becomes pinned to the top
 * of its nearest scrollable ancestor by `position: sticky; top: 0`.
 *
 * Implementation uses IntersectionObserver with a 1px negative top rootMargin:
 * while the host is at its natural position it's fully inside the (shrunk)
 * root, so intersectionRatio stays at 1. Once scrolled and stuck at top: 0,
 * the top 1px of the host falls outside the shrunk root, intersectionRatio
 * drops below 1, and the class flips on.
 */
@Directive({
  selector: '[stickyAware]',
  standalone: true,
})
export class StickyAwareDirective implements OnInit, OnDestroy {
  private readonly el: ElementRef<HTMLElement> = inject(ElementRef);
  private observer?: IntersectionObserver;

  ngOnInit(): void {
    const root = this.findScrollParent(this.el.nativeElement);
    this.observer = new IntersectionObserver(
      ([entry]) => {
        this.el.nativeElement.classList.toggle('is-stuck', entry.intersectionRatio < 1);
      },
      { root, rootMargin: '-1px 0px 0px 0px', threshold: [1] },
    );
    this.observer.observe(this.el.nativeElement);
  }

  ngOnDestroy(): void {
    this.observer?.disconnect();
  }

  private findScrollParent(el: HTMLElement): HTMLElement | null {
    let parent: HTMLElement | null = el.parentElement;
    while (parent) {
      const style = getComputedStyle(parent);
      if (style.overflowY === 'auto' || style.overflowY === 'scroll') {
        return parent;
      }
      parent = parent.parentElement;
    }
    return null;
  }
}
