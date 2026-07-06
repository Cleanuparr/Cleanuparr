export interface WindowOption {
  label: string;
  hours: number;
}

export const WINDOWS: WindowOption[] = [
  { label: '24h', hours: 24 },
  { label: '7d', hours: 168 },
  { label: '30d', hours: 720 },
  { label: '1y', hours: 8760 },
];

export function getChartDuration(): number {
  return typeof matchMedia !== 'undefined' && matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 600;
}

export function formatBucketDate(date: string): string {
  return new Date(`${date}T00:00:00`).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
}
