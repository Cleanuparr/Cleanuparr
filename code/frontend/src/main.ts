import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { APP_BASE_HREF } from '@angular/common';
import { isDevMode } from '@angular/core';
import { provideServiceWorker } from '@angular/service-worker';

async function bootstrap() {
  const basePath = (window as any)['_app_base'] || '/';
  
  const app = await bootstrapApplication(AppComponent, {
    providers: [
      {
        provide: APP_BASE_HREF,
        useValue: basePath
      },
      ...appConfig.providers, provideServiceWorker('ngsw-worker.js', {
            enabled: !isDevMode(),
            registrationStrategy: 'registerWhenStable:30000'
          })
    ]
  });

  return app;
}

bootstrap().catch(err => console.error(err));
