import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { QueueCleanerConfig } from '@shared/models/queue-cleaner-config.model';
import { StallRule, SlowRule, CreateStallRuleDto, CreateSlowRuleDto } from '@shared/models/queue-rule.model';

@Injectable({ providedIn: 'root' })
export class QueueCleanerApi {
  private http = inject(HttpClient);

  getConfig(): Observable<QueueCleanerConfig> {
    return this.http.get<QueueCleanerConfig>('/api/configuration/queue_cleaner');
  }

  updateConfig(config: Partial<QueueCleanerConfig>): Observable<void> {
    return this.http.put<void>('/api/configuration/queue_cleaner', config);
  }

  // Stall rules
  getStallRules(): Observable<StallRule[]> {
    return this.http.get<StallRule[]>('/api/queue-rules/stall');
  }

  createStallRule(rule: CreateStallRuleDto): Observable<StallRule> {
    return this.http.post<StallRule>('/api/queue-rules/stall', rule);
  }

  updateStallRule(id: string, rule: CreateStallRuleDto): Observable<StallRule> {
    return this.http.put<StallRule>(`/api/queue-rules/stall/${id}`, rule);
  }

  deleteStallRule(id: string): Observable<void> {
    return this.http.delete<void>(`/api/queue-rules/stall/${id}`);
  }

  // Slow rules
  getSlowRules(): Observable<SlowRule[]> {
    return this.http.get<SlowRule[]>('/api/queue-rules/slow');
  }

  createSlowRule(rule: CreateSlowRuleDto): Observable<SlowRule> {
    return this.http.post<SlowRule>('/api/queue-rules/slow', rule);
  }

  updateSlowRule(id: string, rule: CreateSlowRuleDto): Observable<SlowRule> {
    return this.http.put<SlowRule>(`/api/queue-rules/slow/${id}`, rule);
  }

  deleteSlowRule(id: string): Observable<void> {
    return this.http.delete<void>(`/api/queue-rules/slow/${id}`);
  }
}
