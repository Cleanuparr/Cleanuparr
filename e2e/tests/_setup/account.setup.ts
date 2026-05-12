import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';

setup('reset app for account specs', async () => {
  // Account specs include OIDC config CRUD which expects OIDC to be enabled.
  await restartAppAndWait({ configureOidc: true });
});
