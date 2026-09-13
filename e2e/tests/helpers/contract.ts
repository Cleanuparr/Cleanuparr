import { expect } from '@playwright/test';

const byName = (a: string, b: string) => a.localeCompare(b);

/** Asserts a response carries exactly these top-level keys. */
export function expectKeys(body: unknown, expected: string[]): void {
  expect(Object.keys(body as Record<string, unknown>).sort(byName)).toEqual([...expected].sort(byName));
}
