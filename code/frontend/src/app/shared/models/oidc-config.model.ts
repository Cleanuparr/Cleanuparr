export interface OidcConfig {
  enabled: boolean;
  issuerUrl: string;
  clientId: string;
  clientSecret: string;
  scopes: string;
  authorizedSubject: string;
  providerName: string;
  redirectUrl: string;
}
