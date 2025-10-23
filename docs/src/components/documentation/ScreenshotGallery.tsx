import React, { useState } from 'react';
import Image from '@theme/IdealImage';
import Lightbox from 'yet-another-react-lightbox';
import 'yet-another-react-lightbox/styles.css';
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
  const [lightboxOpen, setLightboxOpen] = useState(false);
  const [currentIndex, setCurrentIndex] = useState(0);

  const openLightbox = (index: number) => {
    setCurrentIndex(index);
    setLightboxOpen(true);
  };

  // Convert screenshots to lightbox slides format
  const slides = screenshots.map(screenshot => ({
    src: screenshot.src,
    alt: screenshot.alt,
    title: screenshot.title,
  }));

  return (
    <>
      <div className={styles.screenshotGallery}>
        {screenshots.map((screenshot, idx) => (
          <div key={idx} className={styles.screenshotItem}>
            <div
              className={styles.screenshotImageWrapper}
              onClick={() => openLightbox(idx)}
              style={{ cursor: 'zoom-in' }}
            >
              <Image
                img={screenshot.src}
                alt={screenshot.alt}
                className={styles.screenshotImage}
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

      <Lightbox
        open={lightboxOpen}
        close={() => setLightboxOpen(false)}
        slides={slides}
        index={currentIndex}
      />
    </>
  );
}
