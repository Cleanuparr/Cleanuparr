export interface DonationMethod {
  id: string;
  title: string;
  description: string;
  icon: string;
  buttonText: string;
  url?: string;
  featured?: boolean;
  accentColor: string;
  type: 'link' | 'modal';
}

export interface CryptoCurrency {
  id: string;
  name: string;
  symbol: string;
  icon: string;
  address: string;
  color: string;
}

export interface AlternativeSupport {
  title: string;
  description: string;
  icon: string;
  link?: string;
  linkText?: string;
}

export const donationMethods: DonationMethod[] = [
  {
    id: 'github',
    title: 'GitHub Sponsors',
    description: 'Support us through GitHub Sponsors with monthly or one-time contributions. This helps us maintain and improve Cleanuparr.',
    icon: '💖',
    buttonText: 'Sponsor on GitHub',
    url: 'https://github.com/sponsors/Cleanuparr',
    featured: true,
    accentColor: 'var(--support-github)',
    type: 'link'
  },
  {
    id: 'buymeacoffee',
    title: 'Buy Me A Coffee',
    description: 'Support us with a coffee! Quick and easy one-time donations to fuel our development efforts.',
    icon: '☕',
    buttonText: 'Buy Me A Coffee',
    url: 'https://buymeacoffee.com/flaminel',
    accentColor: 'var(--support-buymeacoffee)',
    type: 'link'
  },
  {
    id: 'crypto',
    title: 'Cryptocurrency',
    description: 'Support us with Bitcoin, Ethereum, or other cryptocurrencies. Decentralized donations welcome!',
    icon: '₿',
    buttonText: 'View Crypto Options',
    accentColor: 'var(--support-crypto)',
    type: 'modal'
  }
];

export const cryptoCurrencies: CryptoCurrency[] = [
  {
    id: 'bitcoin',
    name: 'Bitcoin',
    symbol: 'BTC',
    icon: '₿',
    address: '36dmTE24ovkLMR2SAevf6jsVS3XHZSgRTk',
    color: '#f7931a'
  },
  {
    id: 'ethereum',
    name: 'Ethereum',
    symbol: 'ETH',
    icon: 'Ξ',
    address: '0xB71b3B1Cc801DcAF76DB7855927dd68A4D310357',
    color: '#627eea'
  }
];

export const alternativeSupport: AlternativeSupport[] = [
  {
    title: 'Star on GitHub',
    description: 'Give us a star on GitHub to help increase visibility and show your support.',
    icon: '⭐',
    link: 'https://github.com/Cleanuparr/Cleanuparr',
    linkText: 'Star the Repository'
  },
  {
    title: 'Report Bugs',
    description: 'Help improve Cleanuparr by reporting bugs and issues you encounter.',
    icon: '🐛',
    link: 'https://github.com/Cleanuparr/Cleanuparr/issues',
    linkText: 'Report an Issue'
  },
  {
    title: 'Join Discord',
    description: 'Join our Discord community to help other users and participate in discussions.',
    icon: '💬',
    link: 'https://discord.gg/SCtMCgtsc4',
    linkText: 'Join Discord'
  },
  {
    title: 'Share the Project',
    description: 'Help spread the word about Cleanuparr by sharing it with friends and communities.',
    icon: '📢'
  },
  {
    title: 'Contribute Code',
    description: 'Submit pull requests to help improve the codebase and add new features.',
    icon: '💻',
    link: 'https://github.com/Cleanuparr/Cleanuparr/pulls',
    linkText: 'View Pull Requests'
  },
  {
    title: 'Write Documentation',
    description: 'Help improve our documentation to make Cleanuparr easier to use for everyone.',
    icon: '📚'
  }
]; 