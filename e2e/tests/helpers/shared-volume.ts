import { chmodSync, mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';

/** Matches the `chmod -R a+rwX` that scripts/setup-test-data.sh applies to test-data. */
const DIRECTORY_MODE = 0o777;
const FILE_MODE = 0o666;

/**
 * `chmodSync` that tolerates EPERM. The torrent-client bind mounts
 * (`test-data/downloads/<client>`) are chowned to PUID=1000 by
 * linuxserver.io entrypoints, while CI's Playwright runner is uid 1001
 * and cannot chmod paths it doesn't own. Mode bits are already 0o777
 * from setup-test-data.sh's `chmod -R a+rwX`, so the chmod is best-effort.
 */
export function chmodIgnoringEPERM(path: string, mode: number): void {
  try {
    chmodSync(path, mode);
  } catch (err) {
    if ((err as { code?: string }).code !== 'EPERM') {
      throw err;
    }
  }
}

/**
 * Creates a directory inside a volume shared with the containers.
 *
 * `mkdir` honours the runner's umask, so a directory created at run time lands at 0755 owned
 * by the runner. Unlinking a file needs write on its parent, so a container running as
 * PUID=1000 then fails to delete anything inside it. Every level this call created is
 * widened, not just the leaf.
 */
export function mkdirShared(directory: string): void {
  const firstCreated = mkdirSync(directory, { recursive: true });

  let current = directory;

  while (true) {
    chmodIgnoringEPERM(current, DIRECTORY_MODE);

    if (firstCreated === undefined || current === firstCreated) {
      return;
    }

    const parent = dirname(current);

    if (parent === current) {
      return;
    }

    current = parent;
  }
}

/**
 * Writes a file inside a volume shared with the containers, creating its directory.
 * See {@link mkdirShared} for why the modes are widened.
 */
export function writeFileShared(path: string, contents: string | Buffer): void {
  mkdirShared(dirname(path));
  writeFileSync(path, contents);
  chmodIgnoringEPERM(path, FILE_MODE);
}
