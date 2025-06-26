import { HttpErrorResponse } from '@angular/common/http';

export interface BackendError {
  error: string;
  traceId?: string;
}

export class ErrorHandlerUtil {
  /**
   * Extract error message from backend response
   * Handles the structured error format from ExceptionMiddleware
   */
  static extractErrorMessage(error: any): string {
    if (error instanceof HttpErrorResponse) {
      // Check for structured backend error response
      if (error.error && typeof error.error === 'object' && error.error.error) {
        return error.error.error;
      }
      
      // Check for simple error message
      if (error.error && typeof error.error === 'string') {
        return error.error;
      }
      
      // Check for message property
      if (error.error && error.error.message) {
        return error.error.message;
      }
      
      // Fallback to HTTP status text or generic message
      if (error.status === 0) {
        return 'Unable to connect to the server. Please check your connection.';
      }
      
      if (error.status >= 400 && error.status < 500) {
        return error.error?.error || error.message || 'Invalid request. Please check your input.';
      }
      
      if (error.status >= 500) {
        return 'Server error occurred. Please try again later.';
      }
      
      return error.message || 'An unexpected error occurred.';
    }
    
    // Handle non-HTTP errors
    if (error && error.message) {
      return error.message;
    }
    
    return 'An unexpected error occurred.';
  }
  
  /**
   * Check if error is a validation error (400 status)
   */
  static isValidationError(error: any): boolean {
    return error instanceof HttpErrorResponse && error.status === 400;
  }
  
  /**
   * Check if error is a network error (status 0)
   */
  static isNetworkError(error: any): boolean {
    return error instanceof HttpErrorResponse && error.status === 0;
  }
  
  /**
   * Extract trace ID from backend error response
   */
  static extractTraceId(error: any): string | null {
    if (error instanceof HttpErrorResponse && error.error && error.error.traceId) {
      return error.error.traceId;
    }
    return null;
  }
  
  /**
   * Determine if an error message represents a user-fixable validation error
   * These should be shown as toast notifications so the user can correct them
   */
  static isUserFixableError(errorMessage: string): boolean {
    // Common validation error patterns that users can fix
    const validationPatterns = [
      /does not exist/i,
      /cannot be empty/i,
      /invalid/i,
      /required/i,
      /must be/i,
      /should not/i,
      /duplicate/i,
      /already exists/i,
      /format/i,
      /expression/i,
    ];
    
    // Network errors should not be shown as toast (shown in LoadingErrorStateComponent instead)
    const networkErrorPatterns = [
      /unable to connect/i,
      /network/i,
      /connection/i,
      /timeout/i,
      /server error/i,
    ];
    
    // Check if it's a network error first
    if (networkErrorPatterns.some(pattern => pattern.test(errorMessage))) {
      return false;
    }
    
    // Check if it matches validation patterns
    return validationPatterns.some(pattern => pattern.test(errorMessage));
  }
} 