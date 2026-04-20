import { Injectable, WritableSignal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class PaginationService {
  getPageSize(key: string, defaultValue: number): number {
    const raw = localStorage.getItem(key);
    if (raw === null) {
      return defaultValue;
    }
    const parsed = parseInt(raw, 10);
    return Number.isFinite(parsed) && parsed > 0 ? parsed : defaultValue;
  }

  setPageSize(key: string, size: number): void {
    localStorage.setItem(key, String(size));
  }

  createPageSizeHandler(
    key: string,
    pageSize: WritableSignal<number>,
    currentPage: WritableSignal<number>,
    reload: () => void,
  ): (size: number) => void {
    return (size: number) => {
      this.setPageSize(key, size);
      pageSize.set(size);
      currentPage.set(1);
      reload();
    };
  }
}
