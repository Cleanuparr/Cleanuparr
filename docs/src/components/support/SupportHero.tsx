import React from 'react';
import { IconHeart } from '@tabler/icons-react';
import styles from './support.module.css';

export default function SupportHero() {
  return (
    <section className={styles.hero}>
      <div className={styles.heroContent}>
        <h1 className={styles.heroTitle}>
          <span className={styles.heroIcon}><IconHeart size={32} stroke={1.5} /></span>
          Support Cleanuparr
        </h1>
        <p className={styles.heroSubtitle}>
          Help us grow Cleanuparr into a full-featured download manager. Your support enables us to maintain the project,
          add new features, and provide better documentation for the community.
        </p>
      </div>
    </section>
  );
}
