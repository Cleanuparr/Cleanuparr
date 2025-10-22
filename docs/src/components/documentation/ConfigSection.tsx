import React from 'react';
import styles from './documentation.module.css';
import { generateIdFromTitle } from './utils';
import { useIdPrefix } from './IdPrefixContext';

interface ConfigSectionProps {
  id?: string;
  title: string;
  description?: string;
  icon?: string;
  badge?: 'required' | 'optional' | 'advanced';
  children: React.ReactNode;
  className?: string;
}

export default function ConfigSection({
  id,
  title,
  description,
  icon,
  badge,
  children,
  className
}: ConfigSectionProps) {
  // Get prefix from context (if within a section that provides it)
  const prefix = useIdPrefix();

  // Generate ID from title if not provided, with optional prefix
  const elementId = id || generateIdFromTitle(title, prefix);

  return (
    <section
      id={elementId}
      className={`${styles.configSection} ${className || ''}`}
    >
      <div className={styles.configHeader}>
        <h3 className={styles.configTitle}>
          {title}
        </h3>
        {badge && (
          <span className={`${styles.configBadge} ${styles[badge]}`}>
            {badge}
          </span>
        )}
      </div>
      {description && (
        <p className={styles.configDescription}>{description}</p>
      )}
      <div>{children}</div>
    </section>
  );
} 