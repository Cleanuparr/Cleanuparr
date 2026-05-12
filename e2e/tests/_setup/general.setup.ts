import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';

setup('reset app for general specs', async () => {
  await restartAppAndWait();
});
