import React from 'react';
import styles from './documentation.module.css';

interface Screenshot {
  src: string;
  alt: string;
  title: string;
  description?: string;
}

interface ScreenshotGalleryProps {
  screenshots: Screenshot[];
}

export function ScreenshotGallery({ screenshots }: ScreenshotGalleryProps) {
  return (
    <div className={styles.screenshotGallery}>
      {screenshots.map((screenshot, idx) => (
        <div key={idx} className={styles.screenshotItem}>
          <div className={styles.screenshotImageWrapper}>
            <img
              src={screenshot.src}
              alt={screenshot.alt}
              className={styles.screenshotImage}
              loading="lazy"
            />
          </div>
          <div className={styles.screenshotContent}>
            <h3 className={styles.screenshotTitle}>{screenshot.title}</h3>
            {screenshot.description && (
              <p className={styles.screenshotDescription}>{screenshot.description}</p>
            )}
          </div>
        </div>
      ))}
    </div>
  );
}
