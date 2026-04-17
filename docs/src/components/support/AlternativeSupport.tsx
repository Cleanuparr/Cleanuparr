import React from 'react';
import { IconHeartHandshake } from '@tabler/icons-react';
import styles from './support.module.css';
import { alternativeSupport } from './config/donationConfig';

export default function AlternativeSupport() {
  return (
    <section className={styles.alternativeSection}>
      <h2 className={styles.alternativeTitle}>
        <IconHeartHandshake size={24} stroke={1.5} />
        {' '}Other Ways to Help
      </h2>
      <p className={styles.alternativeDescription}>
        Not able to contribute financially? No problem! There are many other ways you can help support
        the Cleanuparr project and our community:
      </p>

      <div className={styles.alternativeGrid}>
        {alternativeSupport.map((item, index) => (
          <div key={index} className={styles.alternativeItem}>
            <span
              className={styles.alternativeIcon}
              aria-label={item.title}
            >
              {item.icon}
            </span>
            <div className={styles.alternativeContent}>
              <h4>{item.title}</h4>
              <p>
                {item.description}
                {item.link && item.linkText && (
                  <>
                    {' '}
                    <a
                      href={item.link}
                      target="_blank"
                      rel="noopener noreferrer"
                      className={styles.alternativeLink}
                      aria-label={`${item.linkText} - opens in new tab`}
                    >
                      {item.linkText}
                    </a>
                  </>
                )}
              </p>
            </div>
          </div>
        ))}
      </div>

      <div style={{ textAlign: 'center', marginTop: '2rem', padding: '2rem', background: 'var(--ifm-color-emphasis-100)', borderRadius: '12px' }}>
        <h3 style={{ margin: '0 0 1rem 0', color: 'var(--support-text-color)' }}>
          Thank You!
        </h3>
        <p style={{ margin: 0, opacity: 0.9, fontSize: '1.1rem', lineHeight: 1.6 }}>
          Every contribution, whether financial or through community participation, helps make Cleanuparr better for everyone.
          We're grateful for your support and involvement in our open-source community.
        </p>
      </div>
    </section>
  );
}
