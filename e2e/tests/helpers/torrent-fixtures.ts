import { createHash, randomBytes } from 'node:crypto';
import { chmodSync, mkdirSync, readdirSync, rmSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

/**
 * Bencode an arbitrary value. Supports integers, Buffers, strings (utf-8),
 * arrays, and plain objects (whose keys are sorted lexicographically as
 * required by BEP-3).
 */
function bencode(value: unknown): Buffer {
  if (typeof value === 'number') {
    if (!Number.isInteger(value)) {
      throw new Error(`bencode: non-integer number ${value}`);
    }
    return Buffer.from(`i${value}e`);
  }
  if (Buffer.isBuffer(value)) {
    return Buffer.concat([Buffer.from(`${value.length}:`), value]);
  }
  if (typeof value === 'string') {
    const buf = Buffer.from(value, 'utf8');
    return Buffer.concat([Buffer.from(`${buf.length}:`), buf]);
  }
  if (Array.isArray(value)) {
    return Buffer.concat([Buffer.from('l'), ...value.map(bencode), Buffer.from('e')]);
  }
  if (value !== null && typeof value === 'object') {
    const obj = value as Record<string, unknown>;
    const keys = Object.keys(obj).sort();
    const parts: Buffer[] = [Buffer.from('d')];
    for (const k of keys) {
      parts.push(bencode(k));
      parts.push(bencode(obj[k]));
    }
    parts.push(Buffer.from('e'));
    return Buffer.concat(parts);
  }
  throw new Error(`bencode: unsupported value ${typeof value}`);
}

export interface GeneratedTorrent {
  /** Bencoded .torrent metainfo buffer */
  metainfo: Buffer;
  /** Lowercase hex SHA-1 of the bencoded info dict — the torrent's infohash */
  infoHash: string;
  /** Name of the top-level directory inside the torrent */
  name: string;
  /** Absolute on-disk path to the directory containing the torrent's data */
  contentPath: string;
}

/**
 * Deterministic bytes seeded from `seed` (HMAC-like SHA-256 expansion)
 */
function generateDeterministicData(seed: Buffer, sizeBytes: number): Buffer {
  const data = Buffer.alloc(sizeBytes);
  let offset = 0;
  let counter = 0;
  while (offset < sizeBytes) {
    const block = createHash('sha256').update(seed).update(Buffer.from([counter & 0xff, (counter >> 8) & 0xff])).digest();
    block.copy(data, offset, 0, Math.min(block.length, sizeBytes - offset));
    offset += block.length;
    counter++;
  }
  return data;
}

/** Concatenated SHA-1 hashes of each `pieceLength`-sized chunk of `data` (BEP-3 `pieces`). */
function computePieces(data: Buffer, pieceLength: number): Buffer {
  const pieces: Buffer[] = [];
  for (let i = 0; i < data.length; i += pieceLength) {
    const piece = data.subarray(i, Math.min(i + pieceLength, data.length));
    pieces.push(createHash('sha1').update(piece).digest());
  }
  return Buffer.concat(pieces);
}

/**
 * Build a single-file multi-piece torrent on disk and return its metainfo.
 *
 * The data file is written to `<savePath>/<name>/data.bin` and contains
 * deterministic random bytes seeded from `name` so re-runs produce the same
 * content (and thus the same infohash) for a given name.
 *
 * @param savePath - directory where the torrent's top-level folder will be created
 * @param name - top-level folder name (also the torrent's `info.name`)
 * @param sizeBytes - total size of the inner data file
 */
export function buildFolderTorrent(savePath: string, name: string, sizeBytes = 32_768, announce = 'http://tracker.invalid/announce'): GeneratedTorrent {
  const contentPath = join(savePath, name);
  mkdirSync(contentPath, { recursive: true });
  chmodIgnoringEPERM(contentPath, 0o777);

  const seed = createHash('sha256').update(`cleanuparr-e2e:${name}`).digest();
  const data = generateDeterministicData(seed, sizeBytes);
  writeFileSync(join(contentPath, 'data.bin'), data);

  const pieceLength = 16384;
  const piecesConcat = computePieces(data, pieceLength);

  const info = {
    name,
    'piece length': pieceLength,
    pieces: piecesConcat,
    files: [
      { length: data.length, path: ['data.bin'] },
    ],
    // Mark as private to short-circuit DHT/PEX work in clients.
    private: 1,
  };
  const metainfo = bencode({
    announce,
    'created by': 'cleanuparr-e2e',
    'creation date': 0,
    info,
  });
  const infoHash = createHash('sha1').update(bencode(info)).digest('hex');

  return { metainfo, infoHash, name, contentPath };
}

/**
 * Build a multi-file torrent (BEP-3 multi-file mode). Each entry in `files`
 * is written as `<savePath>/<name>/<filename>` with deterministic content
 * derived from the torrent name + filename. The pieces are SHA-1 hashes of
 * the concatenated file bytes in declared order, which is how clients verify
 * multi-file torrents.
 */
export function buildMultiFileTorrent(
  savePath: string,
  name: string,
  files: ReadonlyArray<{ filename: string; sizeBytes?: number }>,
): GeneratedTorrent {
  if (files.length === 0) {
    throw new Error('buildMultiFileTorrent: files must be non-empty');
  }

  const contentPath = join(savePath, name);
  mkdirSync(contentPath, { recursive: true });
  chmodIgnoringEPERM(contentPath, 0o777);

  const fileBuffers: Buffer[] = [];
  const fileEntries: Array<{ length: number; path: string[] }> = [];

  for (const { filename, sizeBytes = 16384 } of files) {
    const seed = createHash('sha256').update(`cleanuparr-e2e:${name}:${filename}`).digest();
    const buf = generateDeterministicData(seed, sizeBytes);
    writeFileSync(join(contentPath, filename), buf);
    fileBuffers.push(buf);
    fileEntries.push({ length: buf.length, path: [filename] });
  }

  const concatenated = Buffer.concat(fileBuffers);
  const pieceLength = 16384;
  const piecesConcat = computePieces(concatenated, pieceLength);

  const info = {
    name,
    'piece length': pieceLength,
    pieces: piecesConcat,
    files: fileEntries,
    private: 1,
  };
  const metainfo = bencode({
    announce: 'http://tracker.invalid/announce',
    'created by': 'cleanuparr-e2e',
    'creation date': 0,
    info,
  });
  const infoHash = createHash('sha1').update(bencode(info)).digest('hex');

  return { metainfo, infoHash, name, contentPath };
}

/**
 * Build a single-file torrent (BEP-3 single-file mode — `info.length`, no `files`
 * list and no containing folder). The data file is written directly as
 * `<savePath>/<fileName>`, so the torrent's on-disk entry is the file itself.
 */
export function buildSingleFileTorrent(savePath: string, fileName: string, sizeBytes = 32_768, announce = 'http://tracker.invalid/announce'): GeneratedTorrent {
  mkdirSync(savePath, { recursive: true });
  chmodIgnoringEPERM(savePath, 0o777);

  const seed = createHash('sha256').update(`cleanuparr-e2e:${fileName}`).digest();
  const data = generateDeterministicData(seed, sizeBytes);
  writeFileSync(join(savePath, fileName), data);

  const pieceLength = 16384;
  const piecesConcat = computePieces(data, pieceLength);

  const info = {
    name: fileName,
    'piece length': pieceLength,
    pieces: piecesConcat,
    length: data.length,
    private: 1,
  };
  const metainfo = bencode({
    announce,
    'created by': 'cleanuparr-e2e',
    'creation date': 0,
    info,
  });
  const infoHash = createHash('sha1').update(bencode(info)).digest('hex');

  return { metainfo, infoHash, name: fileName, contentPath: join(savePath, fileName) };
}

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
 * Wipe and recreate a directory. Used at test setup to reset client data.
 */
export function resetDirectory(path: string): void {
  mkdirSync(path, { recursive: true });
  for (const entry of readdirSync(path)) {
    rmSync(join(path, entry), { recursive: true, force: true });
  }
  chmodIgnoringEPERM(path, 0o777);
}

/**
 * Write a random extra file directly under a directory. Useful to seed an
 * unrelated file that the cleaner should classify as orphaned.
 */
export function writeRandomFile(dir: string, name: string, sizeBytes = 1024): string {
  mkdirSync(dir, { recursive: true });
  const path = join(dir, name);
  writeFileSync(path, randomBytes(sizeBytes));
  return path;
}
