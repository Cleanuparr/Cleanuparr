import { Routes } from '@angular/router';
import { pendingChangesGuard } from './core/guards/pending-changes.guard';

export const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', loadComponent: () => import('./dashboard/dashboard-page/dashboard-page.component').then(m => m.DashboardPageComponent) },
  { path: 'logs', loadComponent: () => import('./logging/logs-viewer/logs-viewer.component').then(m => m.LogsViewerComponent) },
  { path: 'events', loadComponent: () => import('./events/events-viewer/events-viewer.component').then(m => m.EventsViewerComponent) },
  { 
    path: 'settings', 
    loadComponent: () => import('./settings/settings-page/settings-page.component').then(m => m.SettingsPageComponent),
    canDeactivate: [pendingChangesGuard] 
  },
  { path: 'sonarr', loadComponent: () => import('./settings/sonarr/sonarr-settings.component').then(m => m.SonarrSettingsComponent) },
  { path: 'radarr', loadComponent: () => import('./settings/radarr/radarr-settings.component').then(m => m.RadarrSettingsComponent) },
  { path: 'lidarr', loadComponent: () => import('./settings/lidarr/lidarr-settings.component').then(m => m.LidarrSettingsComponent) },
  { path: 'readarr', loadComponent: () => import('./settings/readarr/readarr-settings.component').then(m => m.ReadarrSettingsComponent) },
  { path: 'whisparr', loadComponent: () => import('./settings/whisparr/whisparr-settings.component').then(m => m.WhisparrSettingsComponent) },
  { path: 'download-clients', loadComponent: () => import('./settings/download-client/download-client-settings.component').then(m => m.DownloadClientSettingsComponent) },
  { path: 'notifications', loadComponent: () => import('./settings/notification-settings/notification-settings.component').then(m => m.NotificationSettingsComponent) },
];
