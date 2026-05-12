import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';

setup('reset app for seeker specs', async () => {
  await restartAppAndWait();
});
