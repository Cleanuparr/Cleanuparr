import { expect } from '@playwright/test';

/** Asserts a response carries exactly these top-level keys. */
export function expectKeys(body: unknown, expected: string[]): void {
  expect(Object.keys(body as Record<string, unknown>).sort()).toEqual([...expected].sort());
}
