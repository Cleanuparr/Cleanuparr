import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideIcons } from '@ng-icons/core';
import {
  tablerLayoutDashboard,
  tablerFileText,
  tablerBell,
  tablerSettings,
  tablerPlaylistX,
  tablerShieldLock,
  tablerTrash,
  tablerBan,
  tablerDownload,
  tablerBellRinging,
  tablerChevronDown,
  tablerChevronRight,
  tablerMenu2,
  tablerLayoutSidebarLeftCollapse,
  tablerSun,
  tablerMoon,
  tablerCheck,
  tablerBrandGithub,
  tablerBrandDiscord,
  tablerHeart,
  tablerStar,
  tablerExternalLink,
  tablerQuestionMark,
  tablerPencil,
  tablerPlus,
  tablerAlertTriangle,
  tablerX,
  tablerEye,
  tablerEyeOff,
} from '@ng-icons/tabler-icons';

import { routes } from './app.routes';
import { authInterceptor } from '@core/auth/auth.interceptor';
import { baseUrlInterceptor } from '@core/interceptors/base-url.interceptor';
import { errorInterceptor } from '@core/interceptors/error.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withInterceptors([baseUrlInterceptor, authInterceptor, errorInterceptor])),
    provideAnimationsAsync(),
    provideIcons({
      tablerLayoutDashboard,
      tablerFileText,
      tablerBell,
      tablerSettings,
      tablerPlaylistX,
      tablerShieldLock,
      tablerTrash,
      tablerBan,
      tablerDownload,
      tablerBellRinging,
      tablerChevronDown,
      tablerChevronRight,
      tablerMenu2,
      tablerLayoutSidebarLeftCollapse,
      tablerSun,
      tablerMoon,
      tablerCheck,
      tablerBrandGithub,
      tablerBrandDiscord,
      tablerHeart,
      tablerStar,
      tablerExternalLink,
      tablerQuestionMark,
      tablerPencil,
      tablerPlus,
      tablerAlertTriangle,
      tablerX,
      tablerEye,
      tablerEyeOff,
    }),
  ],
};
