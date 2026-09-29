/**
 * Writes text to the clipboard, rejecting when the browser exposes no Clipboard API (plain-HTTP origins).
 */
export function copyToClipboard(text: string): Promise<void> {
  const clipboard: Clipboard | undefined = navigator.clipboard;
  if (!clipboard) {
    return Promise.reject(new Error('Clipboard API unavailable'));
  }
  return clipboard.writeText(text);
}
