import React from 'react';
import { useColorMode } from '@docusaurus/theme-common';
import styles from './documentation.module.css';

interface App {
  name: string;
  icon?: string;
  iconLight?: string;
  iconDark?: string;
  description?: string;
  url?: string;
}

interface AppCardProps {
  title: string;
  apps: App[];
}

export default function AppCard({ title, apps }: AppCardProps) {
  const { colorMode } = useColorMode();

  const getIconForTheme = (app: App): string | undefined => {
    // If iconLight and iconDark are both provided, use theme-appropriate one
    if (app.iconLight && app.iconDark) {
      return colorMode === 'dark' ? app.iconLight : app.iconDark;
    }
    // Otherwise fall back to the single icon prop
    return app.icon;
  };

  return (
    <div className={styles.appCardSection}>
      <h2 className={styles.appCardTitle}>{title}</h2>
      <div className={styles.appGrid}>
        {apps.map((app) => {
          const iconSrc = getIconForTheme(app);
          return (
            <div key={app.name} className={styles.appCard}>
              {iconSrc && (
                <div className={styles.appIconWrapper}>
                  <img
                    src={iconSrc}
                    alt={`${app.name} logo`}
                    className={styles.appIcon}
                  />
                </div>
              )}
              <h3 className={styles.appName}>{app.name}</h3>
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
          );
        })}
      </div>
    </div>
  );
}
