import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';

setup('reset app for oidc specs (with OIDC configured)', async () => {
  await restartAppAndWait({ configureOidc: true });
});
