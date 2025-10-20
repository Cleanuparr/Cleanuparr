import React from 'react';
import styles from './documentation.module.css';

interface SectionTitleProps {
  id?: string;
  icon?: string;
  children: React.ReactNode;
  className?: string;
}

/**
 * Generates a URL-friendly ID from a title string
 * Example: "Connection Settings" -> "connection-settings"
 */
function generateIdFromTitle(title: string): string {
  return title
    .toLowerCase()
    .replace(/[^a-z0-9\s-]/g, '') // Remove special characters
    .replace(/\s+/g, '-')          // Replace spaces with hyphens
    .replace(/-+/g, '-')           // Replace multiple hyphens with single
    .trim();
}

/**
 * Extract text content from React children to generate an ID
 */
function extractTextFromChildren(children: React.ReactNode): string {
  if (typeof children === 'string') {
    return children;
  }
  if (Array.isArray(children)) {
    return children.map(child => extractTextFromChildren(child)).join('');
  }
  if (React.isValidElement(children) && children.props.children) {
    return extractTextFromChildren(children.props.children);
  }
  return '';
}

export default function SectionTitle({
  id,
  icon,
  children,
  className
}: SectionTitleProps) {
  // Generate ID from children text if not provided
  const text = extractTextFromChildren(children);
  const elementId = id || generateIdFromTitle(text);

  return (
    <h2 id={elementId} className={`${styles.sectionTitle} ${className || ''}`}>
      {icon && (
        <span className={styles.sectionIcon} role="img" aria-label={text}>
          {icon}
        </span>
      )}
      {children}
    </h2>
  );
}
