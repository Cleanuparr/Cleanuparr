import { Routes } from '@angular/router';
import { ShellComponent } from '@layout/shell/shell.component';
import { AuthLayoutComponent } from '@layout/auth-layout/auth-layout.component';
import { authGuard, setupIncompleteGuard, loginGuard } from '@core/auth/auth.guard';
import { pendingChangesGuard } from '@core/guards/pending-changes.guard';

export const routes: Routes = [
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('@features/dashboard/dashboard.component').then(
            (m) => m.DashboardComponent,
          ),
      },
      {
        path: 'logs',
        loadComponent: () =>
          import('@app/features/logs-component/logs.component').then((m) => m.LogsComponent),
      },
      {
        path: 'events',
        loadComponent: () =>
          import('@features/events/events.component').then(
            (m) => m.EventsComponent,
          ),
      },
      {
        path: 'strikes',
        loadComponent: () =>
          import('@features/strikes/strikes.component').then(
            (m) => m.StrikesComponent,
          ),
      },
      {
        path: 'settings',
        children: [
          {
            path: 'general',
            loadComponent: () =>
              import(
                '@features/settings/general/general-settings.component'
              ).then((m) => m.GeneralSettingsComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'queue-cleaner',
            loadComponent: () =>
              import(
                '@features/settings/queue-cleaner/queue-cleaner.component'
              ).then((m) => m.QueueCleanerComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'malware-blocker',
            loadComponent: () =>
              import(
                '@features/settings/malware-blocker/malware-blocker.component'
              ).then((m) => m.MalwareBlockerComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'download-cleaner',
            loadComponent: () =>
              import(
                '@features/settings/download-cleaner/download-cleaner.component'
              ).then((m) => m.DownloadCleanerComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'blacklist-sync',
            loadComponent: () =>
              import(
                '@features/settings/blacklist-sync/blacklist-sync.component'
              ).then((m) => m.BlacklistSyncComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'arr/:type',
            loadComponent: () =>
              import(
                '@features/settings/arr/arr-settings.component'
              ).then((m) => m.ArrSettingsComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'download-clients',
            loadComponent: () =>
              import(
                '@features/settings/download-clients/download-clients.component'
              ).then((m) => m.DownloadClientsComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'notifications',
            loadComponent: () =>
              import(
                '@features/settings/notifications/notifications.component'
              ).then((m) => m.NotificationsComponent),
            canDeactivate: [pendingChangesGuard],
          },
          {
            path: 'account',
            loadComponent: () =>
              import(
                '@features/settings/account/account-settings.component'
              ).then((m) => m.AccountSettingsComponent),
          },
        ],
      },
    ],
  },
  {
    path: 'auth',
    component: AuthLayoutComponent,
    children: [
      {
        path: 'login',
        canActivate: [loginGuard],
        loadComponent: () =>
          import('@features/auth/login/login.component').then(
            (m) => m.LoginComponent,
          ),
      },
      {
        path: 'setup',
        canActivate: [setupIncompleteGuard],
        loadComponent: () =>
          import('@features/auth/setup/setup.component').then(
            (m) => m.SetupComponent,
          ),
      },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
