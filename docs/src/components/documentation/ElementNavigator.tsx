import { useEffect } from 'react';
import { useLocation } from '@docusaurus/router';
import styles from './documentation.module.css';

/**
 * Component that handles navigation to specific fields and sections via query parameters
 * Usage: Add ?p=element-id to the URL to scroll to and highlight that element
 */
export default function ElementNavigator() {
  const location = useLocation();

  useEffect(() => {
    // Parse query parameters
    const params = new URLSearchParams(location.search);
    const elementId = params.get('p');

    if (!elementId) {
      return;
    }

    // Wait for the DOM to be fully loaded
    const scrollToElement = () => {
      const element = document.getElementById(elementId);

      if (element) {
        let targetElement = element;

        // If the element is an h2 section title, highlight the parent section container instead
        if (element.tagName === 'H2' && element.classList.contains(styles.sectionTitle)) {
          const parentSection = element.closest(`.${styles.section}`);
          if (parentSection) {
            targetElement = parentSection as HTMLElement;
          }
        }

        // Scroll to the element with offset for header
        targetElement.scrollIntoView({
          behavior: 'smooth',
          block: 'center'
        });

        // Add highlight class
        targetElement.classList.add(styles.highlighted);

        // Remove highlight after animation completes
        setTimeout(() => {
          targetElement.classList.remove(styles.highlighted);
        }, 2000);
      } else {
        console.warn(`Element with id "${elementId}" not found on page`);
      }
    };

    // Use a small delay to ensure content is rendered
    const timeoutId = setTimeout(scrollToElement, 100);

    return () => clearTimeout(timeoutId);
  }, [location.search]);

  // This component doesn't render anything
  return null;
}
