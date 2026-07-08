import type { Mapping, WireMockClient } from './wiremock-client';

export function discordWebhookStub(): Mapping {
  return {
    request: { method: 'POST', urlPathPattern: '/webhooks/.*' },
    response: { status: 204 },
  };
}

export function telegramSendMessageStub(): Mapping {
  return {
    request: { method: 'POST', urlPathPattern: '/bot.*/sendMessage' },
    response: { status: 200, jsonBody: { ok: true, result: { message_id: 1 } } },
  };
}

export function ntfyTopicStub(): Mapping {
  return {
    request: { method: 'POST', urlPathPattern: '/.*' },
    response: { status: 200, jsonBody: { id: 'mock', time: 0, expires: 0, event: 'message' } },
  };
}

export function gotifyMessageStub(): Mapping {
  return {
    request: { method: 'POST', urlPath: '/message' },
    response: { status: 200, jsonBody: { id: 1, message: 'ok' } },
  };
}

export function pushoverMessageStub(): Mapping {
  return {
    request: { method: 'POST', urlPath: '/1/messages.json' },
    response: { status: 200, jsonBody: { status: 1, request: 'mock' } },
  };
}

export function notifiarrStub(): Mapping {
  return {
    request: { method: 'POST', urlPathPattern: '/api/v1/notification/.*' },
    response: { status: 200, jsonBody: { result: 'success' } },
  };
}

export function appriseStub(): Mapping {
  return {
    request: { method: 'POST', urlPathPattern: '/notify/.*' },
    response: { status: 200 },
  };
}

export async function applyNotificationDefaults(notify: WireMockClient): Promise<void> {
  await notify.stubMany([
    discordWebhookStub(),
    telegramSendMessageStub(),
    ntfyTopicStub(),
    gotifyMessageStub(),
    pushoverMessageStub(),
    notifiarrStub(),
    appriseStub(),
  ]);
}
