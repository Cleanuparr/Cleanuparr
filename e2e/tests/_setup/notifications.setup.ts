import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';

setup('reset app for notifications specs', async () => {
  await restartAppAndWait();
});
