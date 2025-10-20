import React from 'react';
import styles from './documentation.module.css';

interface ConfigSectionProps {
  id?: string;
  title: string;
  description?: string;
  icon?: string;
  badge?: 'required' | 'optional' | 'advanced';
  children: React.ReactNode;
  className?: string;
}

/**
 * Generates a URL-friendly ID from a title string
 * Example: "Client Host" -> "client-host"
 */
function generateIdFromTitle(title: string): string {
  return title
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, '') // Remove special characters
    .replace(/\s+/g, '-')          // Replace spaces with hyphens
    .replace(/-+/g, '-')           // Replace multiple hyphens with single
    .trim();
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