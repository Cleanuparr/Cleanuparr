import { signal } from '@angular/core';
import { PaginationService } from './pagination.service';

describe('PaginationService', () => {
  const KEY = 'test-page-size';

  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    localStorage.clear();
  });

  describe('getPageSize', () => {
    it('returns the default when nothing is stored', () => {
      expect(new PaginationService().getPageSize(KEY, 25)).toBe(25);
    });

    it('returns the stored value when it is a valid page size', () => {
      localStorage.setItem(KEY, '50');

      expect(new PaginationService().getPageSize(KEY, 25)).toBe(50);
    });

    it('returns the default for values that are not positive integers', () => {
      const service = new PaginationService();

      for (const stored of ['abc', '0', '-5', '']) {
        localStorage.setItem(KEY, stored);
        expect(service.getPageSize(KEY, 25)).toBe(25);
      }
    });

    it('returns the default when storage throws', () => {
      vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
        throw new Error('SecurityError');
      });

      expect(new PaginationService().getPageSize(KEY, 25)).toBe(25);
    });
  });

  describe('createPageSizeHandler', () => {
    function setup() {
      const pageSize = signal(25);
      const currentPage = signal(4);
      const handler = new PaginationService().createPageSizeHandler(KEY, pageSize, currentPage);
      return { pageSize, currentPage, handler };
    }

    it('persists the size and resets to the first page', () => {
      const { pageSize, currentPage, handler } = setup();

      handler(50);

      expect(pageSize()).toBe(50);
      expect(currentPage()).toBe(1);
      expect(localStorage.getItem(KEY)).toBe('50');
    });

    it('ignores an invalid size entirely', () => {
      const { pageSize, currentPage, handler } = setup();

      handler(0);
      handler(-1);
      handler(12.5);

      expect(pageSize()).toBe(25);
      expect(currentPage()).toBe(4);
      expect(localStorage.getItem(KEY)).toBeNull();
    });

    it('swallows storage failures while still updating the signals', () => {
      vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
        throw new Error('quota exceeded');
      });
      const { pageSize, currentPage, handler } = setup();

      handler(50);

      expect(pageSize()).toBe(50);
      expect(currentPage()).toBe(1);
    });
  });
});
