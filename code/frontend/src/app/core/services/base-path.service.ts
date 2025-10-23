import { Injectable, isDevMode } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class ApplicationPathService {
  
  /**
   * Gets the current base path from the dynamically updated environment
   */
  getBasePath(): string {
    // If in development mode, use the local API
    if (isDevMode()) {
      return `http://localhost:5000`;
    }

    // Use the server-injected base path or fallback to root
    return (window as any)['_server_base_path'] || '/';
  }

  /**
   * Gets the documentation base URL
   */
  getDocumentationBaseUrl(): string {
    if (isDevMode()) {
      return 'http://localhost:3000/Cleanuparr';
    }
    
    return 'https://cleanuparr.github.io/Cleanuparr';
  }

  /**
   * Builds a full URL with the base path
   */
  buildUrl(path: string): string {
    const basePath = this.getBasePath();
    const cleanPath = path.startsWith('/') ? path : '/' + path;
    
    return basePath === '/' ? cleanPath : basePath + cleanPath;
  }

  /**
   * Builds an API URL with the base path
   */
  buildApiUrl(apiPath: string): string {
    const basePath = this.getBasePath();
    const cleanApiPath = apiPath.startsWith('/') ? apiPath : '/' + apiPath;
    
    // In development mode, return full URL directly
    if (isDevMode()) {
      return basePath + '/api' + cleanApiPath;
    }
    
    return basePath === '/' ? '/api' + cleanApiPath : basePath + '/api' + cleanApiPath;
  }

  /**
   * Builds a documentation URL for a specific field
   */
  buildDocumentationUrl(section: string, fieldAnchor?: string): string {
    const baseUrl = this.getDocumentationBaseUrl();
    let url = `${baseUrl}/docs/configuration/${section}`;

    if (fieldAnchor) {
      url += `?${fieldAnchor}`;
    }

    return url;
  }
} 
