import React, { useState } from 'react';
import Image from '@theme/IdealImage';
import Lightbox from 'yet-another-react-lightbox';
import 'yet-another-react-lightbox/styles.css';

interface ClickableImageProps {
  src: string;
  alt: string;
  style?: React.CSSProperties;
  className?: string;
}

export function ClickableImage({ src, alt, style, className }: ClickableImageProps) {
  const [open, setOpen] = useState(false);

  return (
    <>
      <div
        style={{ cursor: 'zoom-in', display: 'inline-block', ...style }}
        className={className}
        onClick={() => setOpen(true)}
      >
        <Image
          img={src}
          alt={alt}
        />
      </div>

      <Lightbox
        open={open}
        close={() => setOpen(false)}
        slides={[{ src: src, alt: alt }]}
      />
    </>
  );
}
