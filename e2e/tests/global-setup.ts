import { waitForKeycloak } from './helpers/keycloak';
import {
  waitForApp,
  createAccountAndSetup,
  loginAndGetToken,
  configureOidc,
} from './helpers/app-api';

async function globalSetup() {
  console.log('Waiting for Keycloak...');
  await waitForKeycloak();
  console.log('Keycloak ready.');

  console.log('Waiting for app...');
  await waitForApp();
  console.log('App ready.');

  console.log('Creating admin account and completing setup...');
  await createAccountAndSetup();

  console.log('Configuring OIDC...');
  const token = await loginAndGetToken();
  await configureOidc(token);
  console.log('Global setup complete.');
}

export default globalSetup;
