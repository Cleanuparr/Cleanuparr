import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';

setup('reset app for queue-cleaner specs', async () => {
  await restartAppAndWait();
});
