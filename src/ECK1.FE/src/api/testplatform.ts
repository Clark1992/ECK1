import { apiFetch } from './client';

const TESTPLATFORM = '/testplatform';

export interface ScenarioSettingDefinition {
  key: string;
  label: string;
  description: string;
  type: 'int' | 'string' | 'select' | 'multiselect';
  defaultValue: unknown;
  min?: number;
  max?: number;
  options?: string[];
}

export interface ScenarioDefinition {
  id: string;
  name: string;
  description: string;
  settings: ScenarioSettingDefinition[];
  stepNames: string[];
}

export interface ScenarioRunResponse {
  runId: string;
  scenarioId: string;
  scenarioName: string;
  resolvedSettings: Record<string, unknown>;
  stepNames: string[];
}

export interface StepProgress {
  stepIndex: number;
  stepName: string;
  status: 'Pending' | 'InProgress' | 'Completed' | 'Failed' | 'Cancelled';
  message?: string;
  data?: Record<string, unknown>;
  startedAt?: string;
  completedAt?: string;
}

export interface ScenarioProgress {
  runId: string;
  scenarioId: string;
  scenarioName: string;
  isRunning: boolean;
  isCompleted: boolean;
  isSuccess: boolean;
  isCancelled: boolean;
  error?: string;
  steps: StepProgress[];
  settings?: Record<string, unknown>;
  startedAt: string;
  completedAt?: string;
}

export interface RunSummary {
  runId: string;
  scenarioId: string;
  scenarioName: string;
  isCompleted: boolean;
  isSuccess: boolean;
  error?: string;
  startedAt: string;
  completedAt?: string;
}

export interface ScenarioCancelResponse {
  runId: string;
  message: string;
}

export const testplatformApi = {
  listScenarios: () => apiFetch<ScenarioDefinition[]>(`${TESTPLATFORM}/api/scenario`),

  getScenario: (id: string) => apiFetch<ScenarioDefinition>(`${TESTPLATFORM}/api/scenario/${id}`),

  runScenario: (scenarioId: string, settings?: Record<string, unknown>) =>
    apiFetch<ScenarioRunResponse>(`${TESTPLATFORM}/api/scenario/run`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ scenarioId, settings }),
    }),

  getRecentRuns: (count = 50) =>
    apiFetch<RunSummary[]>(`${TESTPLATFORM}/api/scenario/runs?count=${count}`),

  getRunProgress: (runId: string) =>
    apiFetch<ScenarioProgress>(`${TESTPLATFORM}/api/scenario/runs/${runId}`),

  cancelRun: (runId: string) =>
    apiFetch<ScenarioCancelResponse>(`${TESTPLATFORM}/api/scenario/runs/${runId}/cancel`, {
      method: 'POST',
    }),
};
