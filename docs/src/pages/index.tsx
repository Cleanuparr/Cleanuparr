import type { ReactNode } from "react";
import clsx from "clsx";
import Link from "@docusaurus/Link";
import useDocusaurusContext from "@docusaurus/useDocusaurusContext";
import Layout from "@theme/Layout";
import Heading from "@theme/Heading";
import SupportBanner from "../components/support/SupportBanner";

import styles from "./index.module.css";

interface FeatureCardProps {
  icon: string;
  title: string;
  description: string;
  color: string;
}

function FeatureCard({ icon, title, description, color }: FeatureCardProps) {
  return (
    <div className={clsx(styles.featureCard)} style={{ '--accent-color': color } as any}>
      <div className={styles.featureIcon}>{icon}</div>
      <h3 className={styles.featureTitle}>{title}</h3>
      <p className={styles.featureDescription}>{description}</p>
    </div>
  );
}

interface QuickStartCardProps {
  icon: string;
  title: string;
  description: string;
  command: string;
  buttonText: string;
  buttonLink: string;
}

function QuickStartCard({ icon, title, description, command, buttonText, buttonLink }: QuickStartCardProps) {
  return (
    <div className={styles.quickStartCard}>
      <div className={styles.quickStartHeader}>
        <span className={styles.quickStartIcon}>{icon}</span>
        <h3 className={styles.quickStartTitle}>{title}</h3>
      </div>
      <p className={styles.quickStartDescription}>{description}</p>
      <div className={styles.commandBlock}>
        <code>{command}</code>
      </div>
      <Link
        className="button button--outline button--primary"
        to={buttonLink}
      >
        {buttonText}
      </Link>
    </div>
  );
}

function HomepageHeader() {
  const { siteConfig } = useDocusaurusContext();
  return (
    <header className={clsx("hero", styles.heroBanner)}>
      <div className={styles.heroBackground}></div>
      <div className="container">
        <div className={styles.heroContent}>
          <div className={styles.heroText}>
            <Heading as="h1" className={styles.heroTitle}>
              <span className={styles.heroTitleMain}>{siteConfig.title}</span>
              <span className={styles.heroTitleSub}>Automated Download Management</span>
            </Heading>
            <p className={styles.heroSubtitle}>
              Automatically clean up unwanted, stalled, and malicious downloads from your *arr applications and download clients. 
              Keep your queues clean and your media library safe.
            </p>
            <div className={styles.heroButtons}>
              <Link
                className="button button--primary button--lg"
                to="/docs/installation"
              >
                🚀 Get Started
              </Link>
              <Link
                className="button button--secondary button--outline button--lg"
                to="/docs/features"
              >
                ✨ View Features
              </Link>
            </div>
          </div>
          <div className={styles.heroVisual}>
            <div className={styles.heroStats}>
              <div className={styles.statItem}>
                <div className={styles.statNumber}>🧹</div>
                <div className={styles.statLabel}>Auto Cleanup</div>
              </div>
              <div className={styles.statItem}>
                <div className={styles.statNumber}>⚡</div>
                <div className={styles.statLabel}>Strike System</div>
              </div>
              <div className={styles.statItem}>
                <div className={styles.statNumber}>🛡️</div>
                <div className={styles.statLabel}>Malware Protection</div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </header>
  );
}

function FeaturesSection() {
  const features: FeatureCardProps[] = [
    {
      icon: "🚫",
      title: "Content Blocking",
      description: "Automatically block and remove malicious files using customizable blocklists and whitelists.",
      color: "#dc3545"
    },
    {
      icon: "⚡",
      title: "Strike System",
      description: "Intelligent strike-based removal for failed imports, stalled downloads, and slow transfers.",
      color: "#ffc107"
    },
    {
      icon: "🔍",
      title: "Auto Search",
      description: "Automatically trigger replacement searches when problematic downloads are removed.",
      color: "#28a745"
    },
    {
      icon: "🌱",
      title: "Seeding Management",
      description: "Clean up completed downloads based on seeding time and ratio requirements.",
      color: "#17a2b8"
    },
    {
      icon: "🔗",
      title: "Orphaned Detection",
      description: "Remove downloads no longer referenced by your *arr applications with hardlink checking.",
      color: "#6f42c1"
    },
    {
      icon: "🔔",
      title: "Smart Notifications",
      description: "Get alerted about strikes, removals, and cleanup operations via Discord or Apprise.",
      color: "#fd7e14"
    }
  ];

  return (
    <section className={styles.featuresSection}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <h2>Why Choose Cleanuparr?</h2>
          <p>Powerful automation features to keep your download ecosystem clean and efficient.</p>
        </div>
        <div className={styles.featuresGrid}>
          {features.map((props, idx) => (
            <FeatureCard key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}

function QuickStartSection() {
  const quickStartOptions: QuickStartCardProps[] = [
    {
      icon: "🐳",
      title: "Docker (Recommended)",
      description: "Get up and running in seconds with Docker Compose",
      command: "docker run -d --name cleanuparr -p 11011:11011 cleanuparr/cleanuparr:latest",
      buttonText: "Docker Setup Guide",
      buttonLink: "/docs/installation"
    },
    {
      icon: "💻",
      title: "Standalone Application",
      description: "Download pre-built binaries for Windows, macOS, and Linux",
      command: "# Download from GitHub Releases\n# Extract and run the executable",
      buttonText: "Setup Guide",
      buttonLink: "/docs/installation/detailed"
    }
  ];

  return (
    <section className={styles.quickStartSection}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <h2>Quick Start</h2>
          <p>Choose your preferred installation method and get started immediately.</p>
        </div>
        <div className={styles.quickStartGrid}>
          {quickStartOptions.map((props, idx) => (
            <QuickStartCard key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}

function IntegrationsSection() {
  const supportedApps = [
    { name: "Sonarr", icon: "📺", color: "#3578e5" },
    { name: "Radarr", icon: "🎬", color: "#ffc107" },
    { name: "Lidarr", icon: "🎵", color: "#28a745" },
    { name: "Readarr", icon: "📚", color: "#6f42c1" },
    { name: "Whisparr", icon: "🔞", color: "#dc3545" },
    { name: "qBittorrent", icon: "⬇️", color: "#17a2b8" },
    { name: "Deluge", icon: "🌊", color: "#fd7e14" },
    { name: "Transmission", icon: "📡", color: "#e83e8c" },
    { name: "µTorrent", icon: "🌀", color: "#343a40" },
  ];

  return (
    <section className={styles.integrationsSection}>
      <div className="container">
        <div className={styles.sectionHeader}>
          <h2>Seamless Integrations</h2>
          <p>Works with all your favorite *arr applications and download clients.</p>
        </div>
        <div className={styles.integrationsGrid}>
          {supportedApps.map((app, idx) => (
            <div key={idx} className={styles.integrationItem} style={{ '--app-color': app.color } as any}>
              <span className={styles.integrationIcon}>{app.icon}</span>
              <span className={styles.integrationName}>{app.name}</span>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

export default function Home(): ReactNode {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title={`${siteConfig.title} - Automated Download Management`}
      description="Automatically clean up unwanted, stalled, and malicious downloads from your *arr applications and download clients"
    >
      <HomepageHeader />
      <main>
        <FeaturesSection />
        <QuickStartSection />
        <IntegrationsSection />
        <div className="container" style={{ padding: '2rem 0' }}>
          <SupportBanner />
        </div>
      </main>
    </Layout>
  );
}
