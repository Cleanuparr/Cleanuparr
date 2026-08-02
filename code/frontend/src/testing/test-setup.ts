const jsdomWindow = (globalThis as { jsdom?: { window: Window } }).jsdom?.window;

if (jsdomWindow) {
  for (const key of ['localStorage', 'sessionStorage'] as const) {
    Object.defineProperty(globalThis, key, { value: jsdomWindow[key], configurable: true });
  }
}
