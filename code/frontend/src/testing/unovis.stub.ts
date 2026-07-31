export function createUnovisStub(): Record<string, unknown> {
  const stubs = new Map<string, unknown>();
  return new Proxy({} as Record<string, unknown>, {
    has: () => true,
    get: (_target, property) => {
      if (typeof property !== 'string' || property === 'then') {
        return undefined;
      }
      if (!stubs.has(property)) {
        stubs.set(property, class {});
      }
      return stubs.get(property);
    },
  });
}
