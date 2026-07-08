import { test as setup } from '@playwright/test';
import { restartAppAndWait } from '../helpers/test-lifecycle';

setup('reset app for core specs', async () => {
  await restartAppAndWait();
});
