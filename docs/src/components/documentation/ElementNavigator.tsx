import { useEffect } from 'react';
import { useLocation } from '@docusaurus/router';
import styles from './documentation.module.css';

/**
 * Component that handles navigation to specific fields and sections via query parameters
 * Usage: Add ?element-id to the URL to scroll to and highlight that element
 */
export default function ElementNavigator() {
  const location = useLocation();

  // Function to perform the scroll and highlight
  const performScroll = (elementId: string) => {
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
      
      return true;
    }
    return false;
  };

  // Function to retry scrolling with multiple attempts
  const scrollToElementWithRetry = (elementId: string, maxAttempts = 10) => {
    let attemptCount = 0;
    
    const tryScroll = () => {
      if (performScroll(elementId)) {
        return; // Success!
      }
      
      attemptCount++;

      // Retry if we haven't exceeded max attempts
      if (attemptCount < maxAttempts) {
        setTimeout(tryScroll, 100);
      }
    };

    // Start trying immediately, then retry if needed
    tryScroll();
  };

  // Effect that listens to URL changes
  useEffect(() => {
    // Parse query parameters
    const params = new URLSearchParams(location.search);

    // Use the first query parameter key as the element ID
    const firstParam = params.keys().next();
    const elementId = firstParam.done ? null : firstParam.value;

    if (elementId) {
      scrollToElementWithRetry(elementId);
    }
  }, [location.search, location.pathname]);

  // Effect that listens to custom event from Root.tsx
  useEffect(() => {
    const handleElementNavigate = (event: CustomEvent) => {
      const elementId = event.detail?.elementId;
      if (elementId) {
        // Use longer delay and more attempts for programmatic navigation
        setTimeout(() => {
          scrollToElementWithRetry(elementId, 10);
        }, 200);
      }
    };

    window.addEventListener('elementNavigate', handleElementNavigate as EventListener);

    return () => {
      window.removeEventListener('elementNavigate', handleElementNavigate as EventListener);
    };
  }, []);

  // This component doesn't render anything
  return null;
}
