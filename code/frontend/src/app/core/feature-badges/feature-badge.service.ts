import { Injectable, inject, signal } from '@angular/core';
import { FeatureViewsApi } from '@core/api/feature-views.api';
import {
  DEFAULT_NEW_BADGE_DURATION_DAYS,
  NEW_FEATURES,
  NewFeature,
} from './feature-registry';

const VIEWS_KEY = 'cleanuparr-feature-views';
const ANCHOR_KEY = 'cleanuparr-feature-anchor';
const DAY_MS = 24 * 60 * 60 * 1000;

/**
 * Grace period after the anchor (account creation) during which a first-seen feature is still
 * treated as pre-existing rather than new. Covers the gap between signing up and the first app
 * load, so brand-new users are not flooded with badges for the features that already existed.
 */
const ANCHOR_GRACE_MS = DAY_MS;

/**
 * Drives the "NEW" feature badges. On every app load it eagerly records a per-user "first seen"
 * timestamp for each registered feature id (so the page a feature lives on is irrelevant). A
 * feature is shown as NEW only if it was first seen more than {@link ANCHOR_GRACE_MS} after the
 * user's account was created — i.e. it appeared after they started using the app — and only for
 * the feature's show duration measured from that first load. Timestamps are stored per user on
 * the backend, falling back to localStorage when no authenticated user exists (e.g.
 * trusted-network auth bypass).
 */
@Injectable({ providedIn: 'root' })
export class FeatureBadgeService {
  private readonly api = inject(FeatureViewsApi);

  private readonly _firstSeen = signal<Record<string, number>>({});
  private readonly _anchor = signal<number | null>(null);
  readonly firstSeen = this._firstSeen.asReadonly();

  private initialized = false;

  init(): void {
    if (this.initialized) {
      return;
    }
    this.initialized = true;

    const featureIds = NEW_FEATURES.map((feature) => feature.id);
    if (featureIds.length === 0) {
      return;
    }

    this.api.record(featureIds).subscribe({
      next: (response) => {
        const parsed: Record<string, number> = {};
        for (const [id, iso] of Object.entries(response.views)) {
          parsed[id] = new Date(iso).getTime();
        }
        this._anchor.set(new Date(response.createdAt).getTime());
        this._firstSeen.set(parsed);
      },
      error: () => this.recordLocally(featureIds),
    });
  }

  isNew(featureId: string): boolean {
    const feature = NEW_FEATURES.find((f) => f.id === featureId);
    if (!feature) {
      return false;
    }

    const anchor = this._anchor();
    const firstSeen = this._firstSeen()[featureId];
    if (anchor === null || firstSeen === undefined) {
      return false;
    }

    const appearedAfterAnchor = firstSeen - anchor > ANCHOR_GRACE_MS;
    const withinShowWindow = Date.now() - firstSeen < this.durationMs(feature);
    return appearedAfterAnchor && withinShowWindow;
  }

  private recordLocally(featureIds: string[]): void {
    const now = Date.now();

    let anchor = this.readNumber(ANCHOR_KEY);
    if (anchor === null) {
      anchor = now;
      this.writeNumber(ANCHOR_KEY, anchor);
    }
    this._anchor.set(anchor);

    const stored = this.readViews();
    let changed = false;
    for (const id of featureIds) {
      if (stored[id] === undefined) {
        stored[id] = now;
        changed = true;
      }
    }
    if (changed) {
      this.writeViews(stored);
    }
    this._firstSeen.set(stored);
  }

  private readViews(): Record<string, number> {
    try {
      const raw = localStorage.getItem(VIEWS_KEY);
      return raw ? JSON.parse(raw) : {};
    } catch {
      return {};
    }
  }

  private writeViews(map: Record<string, number>): void {
    try {
      localStorage.setItem(VIEWS_KEY, JSON.stringify(map));
    } catch {
      // Ignore storage errors (e.g. SecurityError in private mode, quota exceeded).
    }
  }

  private readNumber(key: string): number | null {
    try {
      const raw = localStorage.getItem(key);
      if (raw === null) {
        return null;
      }
      const parsed = Number(raw);
      return Number.isFinite(parsed) ? parsed : null;
    } catch {
      return null;
    }
  }

  private writeNumber(key: string, value: number): void {
    try {
      localStorage.setItem(key, String(value));
    } catch {
      // Ignore storage errors (e.g. SecurityError in private mode, quota exceeded).
    }
  }

  private durationMs(feature: NewFeature): number {
    return (feature.durationDays ?? DEFAULT_NEW_BADGE_DURATION_DAYS) * DAY_MS;
  }
}
