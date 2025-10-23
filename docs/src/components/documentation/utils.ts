/**
 * Generates a URL-friendly ID from a title string
 * Example: "Client Host" -> "client-host"
 *
 * @param title - The title string to convert
 * @param prefix - Optional prefix to prepend to the ID
 * @returns A URL-friendly ID string
 */
export function generateIdFromTitle(title: string, prefix?: string): string {
  const baseId = title
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, '') // Remove special characters
    .replace(/\s+/g, '-')          // Replace spaces with hyphens
    .replace(/-+/g, '-')           // Replace multiple hyphens with single
    .trim();

  return prefix ? `${prefix}-${baseId}` : baseId;
}
