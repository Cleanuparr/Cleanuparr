import { expect, type Page, type Locator } from '@playwright/test';
import { TEST_CONFIG } from './test-config';

type Scope = Page | Locator;

/**
 * Log in through the browser UI and land on the dashboard.
 * Mirrors the flow used by the OIDC UI specs.
 */
export async function loginViaBrowser(page: Page): Promise<void> {
  await page.goto(`${TEST_CONFIG.appUrl}/auth/login`);
  await page.getByRole('textbox', { name: 'Username' }).fill(TEST_CONFIG.adminUsername);
  await page.getByRole('textbox', { name: 'Password' }).fill(TEST_CONFIG.adminPassword);
  await page.getByRole('button', { name: 'Sign In', exact: true }).click();
  await expect(page).toHaveURL(/\/dashboard/, { timeout: 15_000 });
}

export async function gotoSettings(page: Page, path: string): Promise<void> {
  await page.goto(`${TEST_CONFIG.appUrl}/settings/${path}`);
}

/** Log in, then navigate directly to a settings sub-page. */
export async function loginAndGotoSettings(page: Page, path: string): Promise<void> {
  await loginViaBrowser(page);
  await gotoSettings(page, path);
}

// --- Form-control locators ---------------------------------------------------
// The shared UI inputs/selects do NOT associate their <label> with the control
// (no for/id), so getByLabel does not work. Toggles expose the label via
// aria-label. Everything else is scoped by the stable custom-element tag plus
// the label text, then the inner native control.

/** A toggle (role=switch) located by its label (aria-label). */
export function toggle(scope: Scope, label: string): Locator {
  return scope.getByRole('switch', { name: label, exact: true });
}

/** The native <input> inside an app-input whose label contains `label`. */
export function textInput(scope: Scope, label: string): Locator {
  return scope.locator('app-input').filter({ hasText: label }).locator('input');
}

/** The native number <input> inside an app-number-input whose label contains `label`. */
export function numberInput(scope: Scope, label: string): Locator {
  return scope.locator('app-number-input').filter({ hasText: label }).locator('input');
}

/** The text entry <input> inside an app-chip-input whose label contains `label`. */
export function chipInput(scope: Scope, label: string): Locator {
  return scope.locator('app-chip-input').filter({ hasText: label }).locator('input');
}

/** The combobox trigger button inside an app-select whose label contains `label`. */
export function select(scope: Scope, label: string): Locator {
  return scope.locator('app-select').filter({ hasText: label }).getByRole('combobox');
}

/** Open an app-select (by label) and pick an option by its visible text. */
export async function selectOption(scope: Scope, label: string, optionText: string): Promise<void> {
  const el = scope.locator('app-select').filter({ hasText: label });
  await el.getByRole('combobox').click();
  await el.getByRole('option', { name: optionText, exact: true }).click();
}

/** Read a toggle's checked state. */
export async function isToggleOn(sw: Locator): Promise<boolean> {
  return (await sw.getAttribute('aria-checked')) === 'true';
}

/** Ensure a toggle is in the desired state, clicking only if needed. */
export async function ensureToggle(sw: Locator, on: boolean): Promise<void> {
  if ((await isToggleOn(sw)) !== on) {
    await sw.click();
  }
}

/**
 * Ensure an app-accordion is expanded. Clicks the header only if `probe`
 * (an element inside the accordion body) is not already visible, so it works
 * regardless of the accordion's default state.
 */
export async function ensureAccordionExpanded(page: Page, header: string, probe: Locator): Promise<void> {
  if (!(await probe.first().isVisible().catch(() => false))) {
    await page.getByText(header, { exact: true }).click();
    await expect(probe.first()).toBeVisible({ timeout: 5_000 });
  }
}

/** Add a value to an app-chip-input (types + Enter). */
export async function addChip(scope: Scope, label: string, value: string): Promise<void> {
  const input = chipInput(scope, label);
  await input.fill(value);
  await input.press('Enter');
}

// --- Unsaved-changes navigation guard ---------------------------------------
// pendingChangesGuard shows a confirm alertdialog titled "Unsaved Changes"
// (buttons "Leave" / "Stay") when leaving a dirty settings route via a router
// link. We navigate via the sidebar link so the Angular guard actually fires
// (a full page.goto would bypass canDeactivate).

const GUARD = { name: 'Unsaved Changes' } as const;

/** Click a sidebar router link by its label. */
export function navLink(page: Page, label: string): Locator {
  return page.getByRole('link', { name: label, exact: true });
}

/**
 * Attempt to leave via the sidebar link and assert the unsaved-changes guard
 * appears, then stay on the page.
 */
export async function expectGuardOnLeave(page: Page, linkLabel = 'Dashboard'): Promise<void> {
  await navLink(page, linkLabel).click();
  const dialog = page.getByRole('alertdialog', { name: GUARD.name });
  await expect(dialog).toBeVisible({ timeout: 5_000 });
  await dialog.getByRole('button', { name: 'Stay', exact: true }).click();
  await expect(dialog).toBeHidden();
}

/**
 * Attempt to leave via the sidebar link and assert NO guard appears (the form
 * is not dirty), i.e. navigation proceeds. Returns having navigated away.
 */
export async function expectNoGuardOnLeave(page: Page, linkLabel = 'Dashboard'): Promise<void> {
  await navLink(page, linkLabel).click();
  await expect(page).toHaveURL(new RegExp(`/${linkLabel.toLowerCase()}`), { timeout: 5_000 });
  await expect(page.getByRole('alertdialog', { name: GUARD.name })).toHaveCount(0);
}
