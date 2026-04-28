import React from 'react';
import styles from './support.module.css';
import { DonationMethod } from './config/donationConfig';

interface DonationCardProps {
  method: DonationMethod;
  onOpenModal?: () => void;
}

export default function DonationCard({ method, onOpenModal }: DonationCardProps) {
  const handleClick = () => {
    if (method.type === 'modal' && onOpenModal) {
      onOpenModal();
    } else if (method.type === 'link' && method.url) {
      window.open(method.url, '_blank', 'noopener,noreferrer');
    }
  };

  const cardClass = `${styles.donationCard} ${method.featured ? styles.featured : ''}`;

  // Set CSS custom property for accent color
  const cardStyle = {
    '--card-accent-color': method.accentColor
  } as React.CSSProperties;

  const getButtonClass = () => {
    const baseClass = styles.donateBtn;
    switch (method.id) {
      case 'github':
        return `${baseClass} ${styles.githubBtn}`;
      case 'buymeacoffee':
        return `${baseClass} ${styles.buymeacoffeeBtn}`;
      case 'crypto':
        return `${baseClass} ${styles.cryptoBtn}`;
      default:
        return baseClass;
    }
  };

  return (
    <div className={cardClass} style={cardStyle}>
      <span className={styles.donationIcon} aria-label={method.title}>
        {method.icon}
      </span>
      <h3 className={styles.donationTitle}>{method.title}</h3>
      <p className={styles.donationDescription}>{method.description}</p>
      <button
        className={getButtonClass()}
        onClick={handleClick}
        aria-label={`${method.buttonText} - ${method.title}`}
      >
        {method.buttonText}
      </button>
    </div>
  );
}
