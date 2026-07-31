import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideIcons } from '@ng-icons/core';
import { APP_ICONS } from './app.icons';

import { routes } from './app.routes';
import { authInterceptor } from '@core/auth/auth.interceptor';
import { baseUrlInterceptor } from '@core/interceptors/base-url.interceptor';
import { errorInterceptor } from '@core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(
      withInterceptors([baseUrlInterceptor, authInterceptor, errorInterceptor]),
    ),
    provideIcons(APP_ICONS),
  ],
};
