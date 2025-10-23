import React from 'react';
import { IdPrefixProvider } from './IdPrefixContext';

interface PrefixedSectionProps {
  /**
   * The prefix to apply to all child ConfigSection IDs
   * Examples: "stalled", "slow", "failed-import"
   */
  prefix: string;
  children: React.ReactNode;
}

/**
 * Wrapper component that provides an ID prefix to all nested ConfigSection components
 *
 * Usage in MDX:
 * ```mdx
 * <PrefixedSection prefix="stalled">
 *   <SectionTitle icon="⏸️">Stalled Download Rules</SectionTitle>
 *   <ConfigSection title="Max Strikes" icon="⚡">
 *     ... (will have ID: stalled-max-strikes)
 *   </ConfigSection>
 * </PrefixedSection>
 * ```
 */
export default function PrefixedSection({ prefix, children }: PrefixedSectionProps) {
  return (
    <IdPrefixProvider prefix={prefix}>
      {children}
    </IdPrefixProvider>
  );
}
