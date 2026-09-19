import { Injectable, isDevMode } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ApplicationPathService {

  getBasePath(): string {
    if (isDevMode()) {
      return 'http://localhost:5000';
    }
    return (window as unknown as { _server_base_path?: string })._server_base_path || '/';
  }

  getDocumentationBaseUrl(): string {
    if (isDevMode()) {
      return 'http://localhost:3000';
    }
    return 'https://cleanuparr.github.io';
  }

  buildHubUrl(hubPath: string): string {
    const basePath = this.getBasePath();
    const cleanPath = hubPath.startsWith('/') ? hubPath : '/' + hubPath;

    if (isDevMode()) {
      return basePath + cleanPath;
    }

    return basePath === '/' ? cleanPath : basePath + cleanPath;
  }

  buildDocumentationUrl(section: string, fieldAnchor?: string): string {
    const baseUrl = this.getDocumentationBaseUrl();
    let url = `${baseUrl}/docs/configuration/${section}`;
    if (fieldAnchor) {
      url += `?${fieldAnchor}`;
    }
    return url;
  }
}
