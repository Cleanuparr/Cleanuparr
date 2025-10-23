import React from 'react';
import styles from './documentation.module.css';

type AdmonitionType = 'note' | 'important' | 'warning';

interface EnhancedAdmonitionProps {
  type: AdmonitionType;
  title?: string;
  children: React.ReactNode;
  className?: string;
}

const admonitionConfig = {
  note: {
    icon: '💡',
    defaultTitle: 'Note'
  },
  important: {
    icon: '⚠️',
    defaultTitle: 'Important'
  },
  warning: {
    icon: '🚨',
    defaultTitle: 'Warning'
  }
};

export default function EnhancedAdmonition({ 
  type, 
  title, 
  children, 
  className 
}: EnhancedAdmonitionProps) {
  const config = admonitionConfig[type];
  const displayTitle = title || config.defaultTitle;

  return (
    <div className={`${styles.enhancedAdmonition} ${styles[type]} ${className || ''}`}>
      <div className={styles.admonitionHeader}>
        <span className={styles.admonitionIcon} role="img" aria-label={displayTitle}>
          {config.icon}
        </span>
        <strong>{displayTitle}</strong>
      </div>
      <div>{children}</div>
    </div>
  );
}

// Convenience components
export function Note({ title, children, className }: Omit<EnhancedAdmonitionProps, 'type'>) {
  return <EnhancedAdmonition type="note" title={title} className={className}>{children}</EnhancedAdmonition>;
}

export function Important({ title, children, className }: Omit<EnhancedAdmonitionProps, 'type'>) {
  return <EnhancedAdmonition type="important" title={title} className={className}>{children}</EnhancedAdmonition>;
}

export function Warning({ title, children, className }: Omit<EnhancedAdmonitionProps, 'type'>) {
  return <EnhancedAdmonition type="warning" title={title} className={className}>{children}</EnhancedAdmonition>;
}

// Legacy exports for backwards compatibility (can be removed later)
export { Note as EnhancedNote };
export { Important as EnhancedImportant };
export { Warning as EnhancedWarning }; 