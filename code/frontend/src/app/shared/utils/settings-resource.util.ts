import { computed, effect, inject, ResourceRef, Signal } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { Observable } from 'rxjs';
import { ToastService } from '@core/services/toast.service';
import { DeferredLoader } from '@shared/utils/loading.util';

export interface SettingsResourceOptions<T, P> {
  /** Reactive params for the load call; re-runs the load whenever they change. */
  params?: () => P;
  load: (params: P) => Observable<T>;
}

export interface SettingsResource<T> {
  resource: ResourceRef<T | undefined>;
  loader: DeferredLoader;
  loadError: Signal<boolean>;
  retry: () => void;
}

/**
 * Wraps an rxResource with the deferred-spinner loading state and error toast
 * shared by every settings page: a `DeferredLoader` driven by the resource's
 * loading state, a `loadError` flag, and a `retry()` that reloads.
 *
 * Must be called from an injection context (e.g. a component constructor).
 */
export function createSettingsResource<T, P = void>(
  options: SettingsResourceOptions<T, P>,
): SettingsResource<T> {
  const toast = inject(ToastService);

  const resource: ResourceRef<T | undefined> = options.params
    ? rxResource({ params: options.params, stream: ({ params }) => options.load(params) })
    : rxResource({ stream: () => options.load(undefined as P) });

  const loader = new DeferredLoader();
  const loadError = computed(() => !!resource.error());

  effect(() => {
    const err = resource.error();
    if (err) {
      toast.error(err.message);
    }
  });

  effect(() => {
    if (resource.isLoading()) {
      loader.start();
    } else {
      loader.stop();
    }
  });

  return {
    resource,
    loader,
    loadError,
    retry: () => resource.reload(),
  };
}
