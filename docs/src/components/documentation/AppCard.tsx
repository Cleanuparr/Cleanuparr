import React from 'react';
import styles from './documentation.module.css';

interface App {
  name: string;
  iconLight?: string;
  iconDark?: string;
  description?: string;
  url?: string;
  color?: string;
}

interface AppCardProps {
  title: string;
  apps: App[];
}

export default function AppCard({ title, apps }: AppCardProps) {
  return (
    <div className={styles.appCardSection}>
      <h2 className={styles.appCardTitle}>{title}</h2>
      <div className={styles.appGrid}>
        {apps.map((app) => (
          <div
            key={app.name}
            className={styles.appCard}
            style={{ '--app-color': app.color || '#3e0d60' } as React.CSSProperties & { '--app-color': string }}
          >
            {app.iconLight && app.iconDark && (
              <>
                <img
                  src={app.iconLight}
                  alt={`${app.name} logo`}
                  className={`${styles.appIcon} ${styles.appIconLight}`}
                />
                <img
                  src={app.iconDark}
                  alt={`${app.name} logo`}
                  className={`${styles.appIcon} ${styles.appIconDark}`}
                />
              </>
            )}
            <span className={styles.appName}>{app.name}</span>
            {app.description && (
              <p className={styles.appDescription}>{app.description}</p>
            )}
            {app.url && (
              <a
                href={app.url}
                target="_blank"
                rel="noopener noreferrer"
                className={styles.appLink}
              >
                Learn more →
              </a>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}
