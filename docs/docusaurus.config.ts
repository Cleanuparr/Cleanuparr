import {themes as prismThemes} from 'prism-react-renderer';
import type {Config} from '@docusaurus/types';
import type * as Preset from '@docusaurus/preset-classic';

const config: Config = {
  title: 'Cleanuparr',
  tagline: 'Cleaning *arrs since \'24.',
  favicon: 'img/favicon.ico',

  url: 'https://cleanuparr.github.io',
  baseUrl: '/Cleanuparr/',

  organizationName: 'Cleanuparr',
  projectName: 'Cleanuparr',

  onBrokenLinks: 'throw',
  onBrokenMarkdownLinks: 'warn',

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      {
        docs: {
          sidebarPath: './sidebars.ts',
        },
        theme: {
          customCss: './src/css/custom.css',
        },
      } satisfies Preset.Options,
    ],
  ],

  plugins: [
    [
      '@docusaurus/plugin-ideal-image',
      {
        quality: 60,
        max: 1030, // max resized image's size.
        min: 640, // min resized image's size. if original is lower, use that size.
        steps: 2, // the max number of images generated between min and max (inclusive)
        disableInDev: false,
      },
    ],
  ],

  themeConfig: {
    colorMode: {
      defaultMode: 'dark',
      disableSwitch: false,
      respectPrefersColorScheme: false,
    },
    navbar: {
      title: 'Cleanuparr',
      logo: {
        alt: 'Cleanuparr Logo',
        src: 'img/cleanuparr.svg',
      },
      items: [
        {
          type: 'docSidebar',
          sidebarId: 'configurationSidebar',
          position: 'left',
          label: 'Docs',
          activeBasePath: '/docs',
        },
        {
          to: '/support',
          label: 'Support',
          position: 'left',
        },
        {
          href: 'https://github.com/Cleanuparr/Cleanuparr',
          label: 'GitHub',
          position: 'right',
        },
        {
          href: 'https://discord.gg/SCtMCgtsc4',
          label: 'Discord',
          position: 'right',
        }
      ],
    },
    footer: {
      style: 'dark',
      links: [],
      copyright: `Copyright © ${new Date().getFullYear()} Cleanuparr. Built with Docusaurus.`,
    },
    prism: {
      theme: prismThemes.github,
      darkTheme: prismThemes.dracula,
    },
    /*
    algolia: {
      // The application ID provided by Algolia
      appId: 'Y4APRVTFUQ',

      apiKey: 'bdaa942f24c8f4ed9893a5b5970405fa',

      indexName: 'flmorgio',

      // Optional: see doc section below
      contextualSearch: true,

      // Optional: Algolia search parameters
      searchParameters: {},

      // Optional: path for search page that enabled by default (`false` to disable it)
      searchPagePath: 'search',

      // Optional: whether the insights feature is enabled or not on Docsearch (`false` by default)
      insights: true,

      //... other Algolia params
    },
    */
  } satisfies Preset.ThemeConfig,
};

export default config;
