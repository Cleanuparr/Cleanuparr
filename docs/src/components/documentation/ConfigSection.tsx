import React from 'react';
import styles from './documentation.module.css';
import { generateIdFromTitle } from './utils';

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
  // Generate ID from title if not provided
  const elementId = id || generateIdFromTitle(title);

  return (
    <section
      id={elementId}
      className={`${styles.configSection} ${className || ''}`}
    >
      <div className={styles.configHeader}>
        <h3 className={styles.configTitle}>
          {icon && (
            <span className={styles.configIcon} role="img" aria-label={title}>
              {icon}
            </span>
          )}
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