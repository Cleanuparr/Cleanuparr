/**
 * Generates a URL-friendly ID from a title string
 * Example: "Client Host" -> "client-host"
 *
 * @param title - The title string to convert
 * @returns A URL-friendly ID string
 */
export function generateIdFromTitle(title: string): string {
  return title
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, '') // Remove special characters
    .replace(/\s+/g, '-')          // Replace spaces with hyphens
    .replace(/-+/g, '-')           // Replace multiple hyphens with single
    .trim();
}
