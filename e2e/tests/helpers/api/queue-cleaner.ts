import { ApiClient } from './client';

export type QueueRuleKind = 'stall' | 'slow';

export class QueueCleanerApi {
  constructor(private readonly client: ApiClient) {}

  getConfig(): Promise<Response> {
    return this.client.get('/api/configuration/queue_cleaner');
  }

  updateConfig(body: Record<string, unknown>): Promise<Response> {
    return this.client.put('/api/configuration/queue_cleaner', body);
  }

  listRules(kind: QueueRuleKind): Promise<Response> {
    return this.client.get(`/api/queue-rules/${kind}`);
  }

  createRule(kind: QueueRuleKind, body: Record<string, unknown>): Promise<Response> {
    return this.client.post(`/api/queue-rules/${kind}`, body);
  }

  updateRule(kind: QueueRuleKind, id: string, body: Record<string, unknown>): Promise<Response> {
    return this.client.put(`/api/queue-rules/${kind}/${id}`, body);
  }

  deleteRule(kind: QueueRuleKind, id: string): Promise<Response> {
    return this.client.delete(`/api/queue-rules/${kind}/${id}`);
  }
}
