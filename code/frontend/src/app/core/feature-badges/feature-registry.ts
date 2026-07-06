/**
 * Registry of features that should display a "NEW" badge in the UI.
 *
 * To advertise a new feature, add an entry here and set the `featureId` input on the relevant
 * element:
 *   - Box sections (`app-card`, `app-accordion`) render a flame "NEW" corner ribbon in the
 *     top-right corner.
 *   - Field components (`app-toggle`, `app-input`, `app-select`, `app-number-input`,
 *     `app-chip-input`, `app-textarea`, `app-size-input`) render a small flame "NEW" pill next
 *     to their label.
 * You can also drop `<app-new-badge featureId="…">` (ribbon) or
 * `<app-new-badge variant="inline" featureId="…">` (pill) anywhere manually.
 *
 * No dates are needed. The first time a user loads the app while an id is in this list, that
 * "first seen" moment is recorded per user. A feature is treated as NEW only if it was first
 * seen meaningfully after the user's account was created (i.e. it appeared after they started
 * using the app — so brand-new users are never flooded with badges), and it stays visible for
 * `durationDays` (default below) from that first load.
 *
 * Remove an entry once a feature is no longer worth advertising; leaving it costs nothing once
 * every user's show window has elapsed, but pruning keeps this list meaningful.
 */
export interface NewFeature {
  /** Stable, kebab-case identifier. Persisted per user; do not reuse across features. */
  id: string;
  /** Optional override of the default show duration (days the badge is visible after first seen). */
  durationDays?: number;
}

/** Days the badge stays visible after a user first sees it. */
export const DEFAULT_NEW_BADGE_DURATION_DAYS = 7;

export const NEW_FEATURES: NewFeature[] = [
  { id: 'delete-if-any-malware' },
  { id: 'orphaned-files' },
  { id: 'dead-torrent' },
  { id: 'min-seeders' },
  { id: 'unlinked-transmission-label' },
  { id: 'history-retention' },
];
