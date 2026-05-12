import { test, expect } from '../fixtures/base';

// The Discord provider validates the webhook URL against
// `https?://discord(app)?.com/api/webhooks/...` before any HTTP call is made,
// so WireMock cannot intercept it without DNS hijacking. We still verify the
// test endpoint surfaces a failure for an unreachable / fake host — that's
// the realistic signal the UI gets when a user pastes a bogus webhook URL.

test.describe('Notifications — Discord test send', () => {
  test('POST returns failure when webhook URL is unreachable', async ({ api }) => {
    const res = await api.notifications.test('discord', {
      name: 'discord-bad',
      // Valid Discord URL shape so the request passes input validation, but
      // points at a fake snowflake/token combination that Discord rejects.
      webhookUrl: 'https://discord.com/api/webhooks/000000000000000000/cleanuparr-e2e-token',
    });
    expect(res.ok).toBe(false);
  });
});
