import React, { useState, useEffect } from 'react';
import { IconCurrencyBitcoin, IconCheck, IconCopy } from '@tabler/icons-react';
import styles from './support.module.css';
import { cryptoCurrencies, CryptoCurrency } from './config/donationConfig';
import { useClipboard } from './hooks/useClipboard';

interface CryptoModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function CryptoModal({ isOpen, onClose }: CryptoModalProps) {
  const [selectedCrypto, setSelectedCrypto] = useState<CryptoCurrency | null>(null);
  const [step, setStep] = useState<'selection' | 'address'>('selection');
  const { copied, copyToClipboard } = useClipboard();

  // Reset modal state when opened
  useEffect(() => {
    if (isOpen) {
      setStep('selection');
      setSelectedCrypto(null);
    }
  }, [isOpen]);

  // Close modal when clicking outside
  const handleBackdropClick = (e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onClose();
    }
  };

  const handleCryptoSelect = (crypto: CryptoCurrency) => {
    setSelectedCrypto(crypto);
    setStep('address');
  };

  const handleBack = () => {
    setStep('selection');
    setSelectedCrypto(null);
  };

  const handleCopyAddress = async () => {
    if (selectedCrypto) {
      const success = await copyToClipboard(selectedCrypto.address);
      if (success) {
        // Optional: Show toast notification
        console.log('Address copied to clipboard!');
      }
    }
  };

  const generateQRCodeUrl = (address: string) => {
    // Using QR Server API for QR code generation
    return `https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=${encodeURIComponent(address)}`;
  };

  if (!isOpen) return null;

  return (
    <div className={`${styles.modal} ${isOpen ? styles.open : ''}`} onClick={handleBackdropClick}>
      <div className={styles.modalContent}>
        <span className={styles.modalClose} onClick={onClose} aria-label="Close modal">
          &times;
        </span>
        
        {step === 'selection' && (
          <>
            <h2 className={styles.modalTitle}>
              <IconCurrencyBitcoin size={24} stroke={1.5} />
              Choose Cryptocurrency
            </h2>
            <p style={{ textAlign: 'center', marginBottom: '2rem', opacity: 0.8 }}>
              Select your preferred cryptocurrency to view the donation address:
            </p>
            <div className={styles.cryptoSelection}>
              {cryptoCurrencies.map((crypto) => (
                <div
                  key={crypto.id}
                  className={styles.cryptoOption}
                  onClick={() => handleCryptoSelect(crypto)}
                  role="button"
                  tabIndex={0}
                  onKeyDown={(e) => {
                    if (e.key === 'Enter' || e.key === ' ') {
                      handleCryptoSelect(crypto);
                    }
                  }}
                  aria-label={`Select ${crypto.name}`}
                >
                  <span 
                    className={styles.cryptoIcon} 
                    style={{ color: crypto.color }}
                    role="img" 
                    aria-label={crypto.name}
                  >
                    {crypto.icon}
                  </span>
                  <h4>{crypto.name}</h4>
                  <p style={{ margin: 0, fontSize: '0.8rem', opacity: 0.7 }}>
                    {crypto.symbol}
                  </p>
                </div>
              ))}
            </div>
          </>
        )}

        {step === 'address' && selectedCrypto && (
          <>
            <h2 className={styles.modalTitle}>
              <span 
                style={{ color: selectedCrypto.color }}
                role="img" 
                aria-label={selectedCrypto.name}
              >
                {selectedCrypto.icon}
              </span>
              {selectedCrypto.name} Address
            </h2>
            
            <p style={{ textAlign: 'center', marginBottom: '1rem', opacity: 0.8 }}>
              Send {selectedCrypto.name} to this address:
            </p>
            
            <div className={styles.cryptoAddress}>
              {selectedCrypto.address}
            </div>
            
            <div style={{ textAlign: 'center' }}>
              <button 
                className={`${styles.copyBtn} ${copied ? styles.copied : ''}`}
                onClick={handleCopyAddress}
                aria-label={`Copy ${selectedCrypto.name} address to clipboard`}
              >
                {copied ? (
                  <>
                    <IconCheck size={16} stroke={2} />
                    Copied!
                  </>
                ) : (
                  <>
                    <IconCopy size={16} stroke={1.5} />
                    Copy Address
                  </>
                )}
              </button>
            </div>

            <div className={styles.qrCode}>
              <p style={{ margin: '0 0 1rem 0', opacity: 0.8 }}>
                Or scan this QR code:
              </p>
              <div className={styles.qrCodeImage}>
                <img 
                  src={generateQRCodeUrl(selectedCrypto.address)}
                  alt={`QR code for ${selectedCrypto.name} address`}
                  width="200"
                  height="200"
                />
              </div>
            </div>

            <div style={{ textAlign: 'center', marginTop: '2rem' }}>
              <button 
                onClick={handleBack}
                style={{
                  background: 'transparent',
                  border: '1px solid var(--support-border-color)',
                  color: 'var(--support-text-color)',
                  padding: '0.5rem 1rem',
                  borderRadius: '6px',
                  cursor: 'pointer',
                  transition: 'all 0.3s ease'
                }}
                onMouseOver={(e) => {
                  e.currentTarget.style.background = 'var(--support-card-hover-bg)';
                }}
                onMouseOut={(e) => {
                  e.currentTarget.style.background = 'transparent';
                }}
                aria-label="Go back to cryptocurrency selection"
              >
                ← Back to Selection
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
} 