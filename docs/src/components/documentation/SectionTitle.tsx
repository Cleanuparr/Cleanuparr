import React from 'react';
import styles from './documentation.module.css';
import { generateIdFromTitle } from './utils';

interface SectionTitleProps {
  id?: string;
  icon?: string;
  children: React.ReactNode;
  className?: string;
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
      {children}
    </h2>
  );
}
