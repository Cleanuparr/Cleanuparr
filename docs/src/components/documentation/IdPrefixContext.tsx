import React, { createContext, useContext } from 'react';

/**
 * Context for providing ID prefixes to nested ConfigSection components
 * This allows sections like "Stalled Download Rules" to automatically
 * prefix all child ConfigSections with "stalled-"
 */
const IdPrefixContext = createContext<string | undefined>(undefined);

export function useIdPrefix(): string | undefined {
  return useContext(IdPrefixContext);
}

interface IdPrefixProviderProps {
  prefix: string;
  children: React.ReactNode;
}

export function IdPrefixProvider({ prefix, children }: IdPrefixProviderProps) {
  return (
    <IdPrefixContext.Provider value={prefix}>
      {children}
    </IdPrefixContext.Provider>
  );
}
