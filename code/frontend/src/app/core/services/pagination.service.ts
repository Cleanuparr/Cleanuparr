import { Injectable, WritableSignal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PaginationService {
  getPageSize(key: string, defaultValue: number): number {
    try {
      const raw = localStorage.getItem(key);
      if (raw === null) {
        return defaultValue;
      }
      const parsed = parseInt(raw, 10);
      return this.isValidPageSize(parsed) ? parsed : defaultValue;
    } catch {
      return defaultValue;
    }
  }

  setPageSize(key: string, size: number): void {
    try {
      localStorage.setItem(key, String(size));
    } catch {
      // Ignore storage errors (e.g. SecurityError in private mode, quota exceeded).
    }
  }

  createPageSizeHandler(
    key: string,
    pageSize: WritableSignal<number>,
    currentPage: WritableSignal<number>,
    reload: () => void,
  ): (size: number) => void {
    return (size: number) => {
      if (!this.isValidPageSize(size)) {
        return;
      }
      this.setPageSize(key, size);
      pageSize.set(size);
      currentPage.set(1);
      reload();
    };
  }

  private isValidPageSize(size: number): boolean {
    return Number.isInteger(size) && size > 0;
  }
}
