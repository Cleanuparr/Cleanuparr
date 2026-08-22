import { createHmac } from 'node:crypto';

const BASE32_ALPHABET = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';

function base32Decode(base32: string): Buffer {
  const normalized = base32.toUpperCase().replace(/=+$/, '');
  const bits: number[] = [];

  for (const character of normalized) {
    const value = BASE32_ALPHABET.indexOf(character);
    if (value < 0) {
      continue;
    }
    for (let i = 4; i >= 0; i--) {
      bits.push((value >> i) & 1);
    }
  }

  const bytes = Buffer.alloc(Math.floor(bits.length / 8));
  for (let i = 0; i < bytes.length; i++) {
    let byte = 0;
    for (let j = 0; j < 8; j++) {
      byte = (byte << 1) | bits[i * 8 + j];
    }
    bytes[i] = byte;
  }

  return bytes;
}

/** Makes the current RFC 6238 code for a base32 secret. The backend uses 6 digits and 30 seconds. */
export function generateTotpCode(base32Secret: string): string {
  const timestep = Math.floor(Date.now() / 1000 / 30);
  const counter = Buffer.alloc(8);
  counter.writeBigInt64BE(BigInt(timestep));

  const hash = createHmac('sha1', base32Decode(base32Secret)).update(counter).digest();
  const offset = hash[hash.length - 1] & 0x0f;
  const binaryCode =
    ((hash[offset] & 0x7f) << 24) |
    ((hash[offset + 1] & 0xff) << 16) |
    ((hash[offset + 2] & 0xff) << 8) |
    (hash[offset + 3] & 0xff);

  return (binaryCode % 1_000_000).toString().padStart(6, '0');
}
