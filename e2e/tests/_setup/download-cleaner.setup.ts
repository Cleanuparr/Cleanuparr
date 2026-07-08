import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';

setup('reset app for download-cleaner specs', async () => {
  await restartAppAndWait();
});
