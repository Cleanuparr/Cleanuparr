import React from 'react';
import Link from '@docusaurus/Link';

interface SupportBannerProps {
  compact?: boolean;
  showDismiss?: boolean;
  onDismiss?: () => void;
}

export default function SupportBanner({ compact = false, showDismiss = false, onDismiss }: SupportBannerProps) {
  const bannerStyle: React.CSSProperties = {
    background: 'linear-gradient(90deg, #3b82f6, #10b981)',
    color: 'white',
    padding: compact ? '2rem' : '3rem 2rem',
    textAlign: 'center',
    position: 'relative',
  };

  const titleStyle: React.CSSProperties = {
    fontSize: compact ? '1.25rem' : '1.5rem',
    fontWeight: '600',
    margin: '0 0 0.5rem 0',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    gap: '0.5rem',
  };

  const descriptionStyle: React.CSSProperties = {
    margin: compact ? '0 0 1rem 0' : '0 0 1.5rem 0',
    opacity: 0.95,
    fontSize: compact ? '0.9rem' : '1rem',
    lineHeight: 1.5,
  };

  const buttonStyle: React.CSSProperties = {
    background: 'rgba(255, 255, 255, 0.2)',
    color: 'white',
    border: '2px solid rgba(255, 255, 255, 0.3)',
    padding: compact ? '0.5rem 1.5rem' : '0.75rem 2rem',
    borderRadius: '8px',
    textDecoration: 'none',
    fontWeight: '600',
    fontSize: compact ? '0.9rem' : '1rem',
    display: 'inline-flex',
    alignItems: 'center',
    gap: '0.5rem',
    transition: 'all 0.3s ease',
    backdropFilter: 'blur(10px)',
  };

  const dismissStyle: React.CSSProperties = {
    position: 'absolute',
    top: '0.75rem',
    right: '1rem',
    background: 'transparent',
    border: 'none',
    color: 'white',
    fontSize: '1.5rem',
    cursor: 'pointer',
    opacity: 0.7,
    transition: 'opacity 0.3s ease',
  };

  return (
    <div style={bannerStyle}>
      {showDismiss && (
        <button
          style={dismissStyle}
          onClick={onDismiss}
          onMouseOver={(e) => (e.currentTarget.style.opacity = '1')}
          onMouseOut={(e) => (e.currentTarget.style.opacity = '0.7')}
          aria-label="Dismiss support banner"
        >
          ×
        </button>
      )}
      
      <h3 style={titleStyle}>
        <span role="img" aria-label="Heart">❤️</span>
        {compact ? 'Support Cleanuparr' : 'Love Cleanuparr?'}
      </h3>
      
      <p style={descriptionStyle}>
        {compact 
          ? 'Help us keep improving Cleanuparr for everyone!'
          : 'Help us maintain and improve Cleanuparr by supporting our development efforts.'
        }
      </p>
      
      <Link
        to="/support"
        style={buttonStyle}
        onMouseOver={(e) => {
          e.currentTarget.style.background = 'rgba(255, 255, 255, 0.3)';
          e.currentTarget.style.transform = 'translateY(-2px)';
        }}
        onMouseOut={(e) => {
          e.currentTarget.style.background = 'rgba(255, 255, 255, 0.2)';
          e.currentTarget.style.transform = 'translateY(0)';
        }}
      >
        <span role="img" aria-label="Support">🚀</span>
        {compact ? 'Support Us' : 'Support the Project'}
      </Link>
    </div>
  );
} 