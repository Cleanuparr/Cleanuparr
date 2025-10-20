import { useEffect } from 'react';
import { useLocation } from '@docusaurus/router';
import styles from './documentation.module.css';

/**
 * Component that handles navigation to specific fields via query parameters
 * Usage: Add ?p=field-id to the URL to scroll to and highlight that field
 */
export default function FieldNavigator() {
  const location = useLocation();

  useEffect(() => {
    // Parse query parameters
    const params = new URLSearchParams(location.search);
    const fieldId = params.get('p');

    if (!fieldId) {
      return;
    }

    // Wait for the DOM to be fully loaded
    const scrollToField = () => {
      const element = document.getElementById(fieldId);

      if (element) {
        // Scroll to the element with offset for header
        element.scrollIntoView({
          behavior: 'smooth',
          block: 'center'
        });

        // Add highlight class
        element.classList.add(styles.highlighted);

        // Remove highlight after animation completes
        setTimeout(() => {
          element.classList.remove(styles.highlighted);
        }, 2000);
      } else {
        console.warn(`Field with id "${fieldId}" not found on page`);
      }
    };

    // Use a small delay to ensure content is rendered
    const timeoutId = setTimeout(scrollToField, 100);

    return () => clearTimeout(timeoutId);
  }, [location.search]);

  // This component doesn't render anything
  return null;
}
