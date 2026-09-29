import { copyToClipboard } from './clipboard.util';

describe('copyToClipboard', () => {
  afterEach(() => {
    delete (navigator as { clipboard?: Clipboard }).clipboard;
  });

  it('writes the text through the Clipboard API', async () => {
    const writeText = vi.fn(() => Promise.resolve());
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });

    await copyToClipboard('hello');

    expect(writeText).toHaveBeenCalledWith('hello');
  });

  it('rejects instead of throwing when the Clipboard API is missing', async () => {
    await expect(copyToClipboard('hello')).rejects.toThrow('Clipboard API unavailable');
  });
});
