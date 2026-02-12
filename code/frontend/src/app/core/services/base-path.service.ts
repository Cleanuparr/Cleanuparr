import { Injectable, isDevMode } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ApplicationPathService {

  getBasePath(): string {
    if (isDevMode()) {
      return 'http://localhost:5000';
    }
    return (window as any)['_server_base_path'] || '/';
  }

  getDocumentationBaseUrl(): string {
    if (isDevMode()) {
      return 'http://localhost:3000/Cleanuparr';
    }
    return 'https://cleanuparr.github.io/Cleanuparr';
  }

  buildUrl(path: string): string {
    const basePath = this.getBasePath();
    const cleanPath = path.startsWith('/') ? path : '/' + path;
    return basePath === '/' ? cleanPath : basePath + cleanPath;
  }

  buildApiUrl(apiPath: string): string {
    const basePath = this.getBasePath();
    const cleanApiPath = apiPath.startsWith('/') ? apiPath : '/' + apiPath;

    if (isDevMode()) {
      return basePath + '/api' + cleanApiPath;
    }

    return basePath === '/' ? '/api' + cleanApiPath : basePath + '/api' + cleanApiPath;
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
