import { execFileSync } from 'node:child_process';
import { readFileSync } from 'node:fs';
import { basename, join, resolve } from 'node:path';
import { pollUntilOk } from '../torrent-clients/types';
import { mkdirShared, writeFileShared } from '../shared-volume';

/** Host directory bind-mounted as /data in the sabnews container (see docker-compose.e2e.yml). */
const HOST_ARTICLES_DIR = resolve(__dirname, '..', '..', '..', 'test-data', 'sabnews-articles');
const CONTAINER_ARTICLES_DIR = '/data';
const COMPOSE_FILE = resolve(__dirname, '..', '..', '..', 'docker-compose.e2e.yml');

export interface SabnzbdQueueSlot {
  nzo_id: string;
  filename: string;
  status: string;
  cat: string;
}

export interface SabnzbdHistorySlot {
  nzo_id: string;
  name: string;
  status: string;
  storage: string;
  fail_message: string;
  category: string;
}

/**
 * An NZB referencing an article file that does not exist, so SABnzbd fails the job fast.
 *
 * Shared by {@link SabnzbdDriver.addMissingSegmentNzb} (uploaded directly) and the
 * live-lazylibrarian Newznab stub (served over HTTP for SABnzbd's own `addurl` to fetch).
 */
export function buildMissingSegmentNzb(fileName: string): string {
  const missingPath = `${CONTAINER_ARTICLES_DIR}/missing-${fileName}`;
  return [
    '<?xml version="1.0" encoding="UTF-8"?>',
    '<!DOCTYPE nzb PUBLIC "-//newzBin//DTD NZB 1.0//EN" "http://www.newzbin.com/DTD/nzb/nzb-1.0.dtd">',
    '<nzb xmlns="http://www.newzbin.com/DTD/2003/nzb">',
    `<file poster="e2e" date="1700000000" subject="&quot;${fileName}&quot;">`,
    '<groups><group>alt.binaries.test</group></groups>',
    '<segments>',
    `<segment number="1" bytes="500000">file=${missingPath}|part=1|start=0|size=500000</segment>`,
    '</segments>',
    '</file>',
    '</nzb>',
    '',
  ].join('\n');
}

/**
 * Driver for the live SABnzbd container used by the e2e usenet specs.
 *
 * Unlike the torrent drivers, "adding a download" means handing SABnzbd an NZB that
 * resolves against the fake `sabnews` NNTP server, so the job really downloads instead
 * of being faked into existence. `addWorkingNzb` shells out to `docker compose exec` to
 * run SABnzbd's own `tests/sabnews.py --nzbfile` generator inside the sabnews container
 * (it needs to run where the referenced article path resolves). `addMissingSegmentNzb`
 * builds a corrupt NZB by hand instead: no generator round trip needed to make SABnzbd
 * fail to find an article.
 */
export class SabnzbdDriver {
  readonly apiKey: string;
  private readonly host: string;

  constructor(host = 'http://localhost:8070', apiKey = '0000000000000000000000000000e2e5') {
    this.host = host;
    this.apiKey = apiKey;
  }

  /** URL the Cleanuparr backend should be configured with. */
  get cleanuparrHost(): string {
    return this.host;
  }

  async ready(): Promise<void> {
    await pollUntilOk(async () => (await this.call('version')).ok, { label: 'SABnzbd API' });
  }

  private async call(mode: string, params: Record<string, string> = {}): Promise<Response> {
    const query = new URLSearchParams({ mode, output: 'json', apikey: this.apiKey, ...params });
    return fetch(`${this.host}/api?${query.toString()}`);
  }

  private async json<T>(mode: string, params: Record<string, string> = {}): Promise<T> {
    const res = await this.call(mode, params);
    if (!res.ok) {
      throw new Error(`SABnzbd ${mode} failed: ${res.status} ${await res.text()}`);
    }
    return res.json() as Promise<T>;
  }

  /** Builds a real NZB via SABnzbd's own fake-news-server generator and adds it under `category`. */
  async addWorkingNzb(fileName: string, contentBytes: number, category: string): Promise<string> {
    mkdirShared(HOST_ARTICLES_DIR);
    const hostPath = join(HOST_ARTICLES_DIR, fileName);
    writeFileShared(hostPath, Buffer.alloc(contentBytes, 1));

    execFileSync(
      'docker',
      ['compose', '-f', COMPOSE_FILE, 'exec', '-T', 'sabnews', 'python3', '/sabnews.py', '--nzbfile', `${CONTAINER_ARTICLES_DIR}/${fileName}`],
      { stdio: 'pipe' },
    );

    const nzbPath = hostPath.replace(/\.[^.]+$/, '') + '.nzb';
    return this.addNzbFile(nzbPath, category);
  }

  /** Writes a deterministic-size article file under the shared sabnews articles directory. */
  writeArticleFile(dirName: string, fileName: string, sizeBytes: number): void {
    const hostDir = join(HOST_ARTICLES_DIR, dirName);
    mkdirShared(hostDir);
    writeFileShared(join(hostDir, fileName), Buffer.alloc(sizeBytes, 1));
  }

  /**
   * Builds a single NZB covering every file already written under `dirName`
   * (one `<file>` entry per file), so the job lands in one job folder with
   * multiple files: the usenet equivalent of a multi-file torrent.
   */
  async addWorkingNzbDir(dirName: string, category: string): Promise<string> {
    const hostDir = join(HOST_ARTICLES_DIR, dirName);
    execFileSync(
      'docker',
      ['compose', '-f', COMPOSE_FILE, 'exec', '-T', 'sabnews', 'python3', '/sabnews.py', '--nzbdir', `${CONTAINER_ARTICLES_DIR}/${dirName}`],
      { stdio: 'pipe' },
    );
    const nzbPath = join(hostDir, `${dirName}.nzb`);
    return this.addNzbFile(nzbPath, category);
  }

  /** Builds an NZB referencing an article file that does not exist, so SABnzbd fails the job fast. */
  async addMissingSegmentNzb(fileName: string, category: string): Promise<string> {
    mkdirShared(HOST_ARTICLES_DIR);
    const hostNzbPath = join(HOST_ARTICLES_DIR, `${fileName}.nzb`);
    writeFileShared(hostNzbPath, buildMissingSegmentNzb(fileName));
    return this.addNzbFile(hostNzbPath, category);
  }

  private async addNzbFile(nzbPath: string, category: string): Promise<string> {
    const form = new FormData();
    form.append('name', new Blob([readFileSync(nzbPath)]), basename(nzbPath));
    form.append('cat', category);
    // pp=0 (download only, no repair/unpack): fixture files are raw bytes, not
    // real archives, and SABnzbd's unpack step spends up to its own timeout
    // trying to make sense of them before giving up.
    form.append('pp', '0');
    const query = new URLSearchParams({ mode: 'addfile', output: 'json', apikey: this.apiKey });
    const res = await fetch(`${this.host}/api?${query.toString()}`, { method: 'POST', body: form });
    if (!res.ok) {
      throw new Error(`SABnzbd addfile failed: ${res.status} ${await res.text()}`);
    }
    const body = (await res.json()) as { status: boolean; nzo_ids?: string[]; error?: string };
    if (!body.status || !body.nzo_ids?.[0]) {
      throw new Error(`SABnzbd addfile rejected the job: ${body.error ?? JSON.stringify(body)}`);
    }
    return body.nzo_ids[0];
  }

  /** Pauses the whole queue, so a job added afterwards never starts downloading. */
  async pauseQueue(): Promise<void> {
    await this.call('pause');
  }

  async resumeQueue(): Promise<void> {
    await this.call('resume');
  }

  async pauseJob(nzoId: string): Promise<void> {
    await this.call('queue', { name: 'pause', value: nzoId });
  }

  async resumeJob(nzoId: string): Promise<void> {
    await this.call('queue', { name: 'resume', value: nzoId });
  }

  /** Percent of the configured bandwidth cap (0-100). SABnzbd has no per-job speed limit. */
  async setSpeedLimit(percent: number): Promise<void> {
    await this.call('config', { name: 'speedlimit', value: String(percent) });
  }

  /**
   * Creates (or repoints) a category with its own output folder. `dir` is relative to
   * complete_dir unless it starts with `/`, in which case SABnzbd uses it as-is. `name`
   * is required alongside `keyword` or SABnzbd won't create a category that doesn't exist yet.
   */
  async setCategoryDir(category: string, dir: string): Promise<void> {
    await this.call('set_config', { section: 'categories', name: category, keyword: category, dir });
  }

  /** Removes a category added by {@link setCategoryDir}. SABnzbd 5.1.3 only exposes this as `del_config`. */
  async deleteCategory(category: string): Promise<void> {
    await this.call('del_config', { section: 'categories', keyword: category });
  }

  async listQueue(): Promise<SabnzbdQueueSlot[]> {
    const data = await this.json<{ queue: { slots: SabnzbdQueueSlot[] } }>('queue');
    return data.queue.slots;
  }

  async listHistory(): Promise<SabnzbdHistorySlot[]> {
    const data = await this.json<{ history: { slots: SabnzbdHistorySlot[] } }>('history', { limit: '0' });
    return data.history.slots;
  }

  async findJob(nzoId: string): Promise<{ queue?: SabnzbdQueueSlot; history?: SabnzbdHistorySlot }> {
    const [queue, history] = await Promise.all([this.listQueue(), this.listHistory()]);
    return {
      queue: queue.find((s) => s.nzo_id === nzoId),
      history: history.find((s) => s.nzo_id === nzoId),
    };
  }

  /**
   * Polls history until `status` is reached, then waits once more for the same
   * status to settle. A job can briefly show "Completed" in the API while
   * SABnzbd's post-processing (sorting/renaming) is still moving the final
   * file into place, so a single poll hit is not proof the files are done moving.
   */
  async waitForHistoryStatus(nzoId: string, status: string, timeoutMs = 60_000): Promise<SabnzbdHistorySlot> {
    const start = Date.now();
    let slot: SabnzbdHistorySlot | undefined;
    while (Date.now() - start < timeoutMs) {
      const { history } = await this.findJob(nzoId);
      if (history?.status === status) {
        slot = history;
        break;
      }
      await new Promise((r) => setTimeout(r, 1000));
    }
    if (!slot) {
      throw new Error(`Timed out waiting for SABnzbd job ${nzoId} to reach status "${status}"`);
    }
    await new Promise((r) => setTimeout(r, 3000));
    const { history } = await this.findJob(nzoId);
    return history ?? slot;
  }

  async deleteHistoryJob(nzoId: string, deleteFiles: boolean): Promise<void> {
    await this.call('history', { name: 'delete', value: nzoId, del_files: deleteFiles ? '1' : '0', archive: '0' });
  }

  async deleteQueueJob(nzoId: string, deleteFiles: boolean): Promise<void> {
    await this.call('queue', { name: 'delete', value: nzoId, del_files: deleteFiles ? '1' : '0' });
  }

  /** Empties the queue and permanently deletes history (archive=0), so re-runs start clean. */
  async clearAll(): Promise<void> {
    await this.setSpeedLimit(100);
    const [queue, history] = await Promise.all([this.listQueue(), this.listHistory()]);
    await Promise.all(queue.map((s) => this.deleteQueueJob(s.nzo_id, true)));
    await Promise.all(history.map((s) => this.deleteHistoryJob(s.nzo_id, true)));
  }
}
