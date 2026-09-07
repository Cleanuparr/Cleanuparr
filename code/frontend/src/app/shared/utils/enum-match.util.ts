/**
 * Finds the enum member whose value matches, ignoring case.
 * Callers get null for anything the running build does not know.
 */
export function matchEnum<T extends Record<string, string>>(
  members: T,
  value: string | null | undefined,
): T[keyof T] | null {
  if (!value) {
    return null;
  }
  const needle = value.toLowerCase();
  const values = Object.values(members) as T[keyof T][];
  return values.find((member) => member.toLowerCase() === needle) ?? null;
}
