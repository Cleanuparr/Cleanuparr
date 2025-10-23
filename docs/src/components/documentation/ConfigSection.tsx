import React, { useState } from 'react';
import styles from './documentation.module.css';
import { generateIdFromTitle } from './utils';
import { useIdPrefix } from './IdPrefixContext';

interface ConfigSectionProps {
  id?: string;
  title: string;
  description?: string;
  icon?: string;
  badge?: 'required' | 'optional' | 'advanced' | string;
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
  const [copied, setCopied] = useState(false);

  // Get prefix from context (if within a section that provides it)
  const prefix = useIdPrefix();

  // Generate ID from title if not provided, with optional prefix
  const elementId = id || generateIdFromTitle(title, prefix);

  const copyAnchorLink = () => {
    const url = new URL(window.location.href);
    // Remove any existing query params
    url.search = '?' + elementId;

    navigator.clipboard.writeText(url.toString()).then(() => {
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    });
  };

  return (
    <section
      id={elementId}
      className={`${styles.configSection} ${className || ''}`}
    >
      <div className={styles.configHeader}>
        <h3 className={styles.configTitle}>
          {icon && <span className={styles.configIcon}>{icon}</span>}
          {title}
        </h3>
        <div className={styles.configHeaderActions}>
          {badge && (
            <span className={`${styles.configBadge} ${styles[badge] || ''}`}>
              {badge}
            </span>
          )}
          <button
            className={styles.copyAnchorButton}
            onClick={copyAnchorLink}
            title="Copy link to this section"
            aria-label="Copy link to this section"
          >
            {copied ? '✓' : '🔗'}
          </button>
        </div>
      </div>
      {description && (
        <p className={styles.configDescription}>{description}</p>
      )}
      <div>{children}</div>
    </section>
  );
} 