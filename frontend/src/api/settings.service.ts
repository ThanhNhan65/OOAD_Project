import { request } from './apiClient';

export interface AppSettingsSummary {
  reminderHours: number;
  cancelDeadlineHours: number;
  waitlistCutoffHours: number;
  expiryWarningDays: number;
  ptLowSessions: number;
}

export interface ApiSystemSettingDto {
  id: string;
  branchId?: string;
  branchName?: string;
  settingKey: string;
  settingValue: string;
  dataType: string;
  description?: string;
  updatedAt: string;
  updatedByUserName?: string;
}

export interface UpdateSystemSettingPayload {
  settingKey: string;
  settingValue: string;
  dataType?: string;
  description?: string;
  branchId?: string;
}

export const settingsService = {
  async getSummary(branchId?: string): Promise<AppSettingsSummary> {
    const qs = branchId ? `?branchId=${branchId}` : '';
    return request<AppSettingsSummary>(`/settings/summary${qs}`);
  },

  async updateSummary(payload: AppSettingsSummary, branchId?: string): Promise<AppSettingsSummary> {
    const qs = branchId ? `?branchId=${branchId}` : '';
    return request<AppSettingsSummary>(`/settings/summary${qs}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  async getAll(branchId?: string): Promise<ApiSystemSettingDto[]> {
    const qs = branchId ? `?branchId=${branchId}` : '';
    return request<ApiSystemSettingDto[]>(`/settings${qs}`);
  },

  async upsertSetting(payload: UpdateSystemSettingPayload): Promise<ApiSystemSettingDto> {
    return request<ApiSystemSettingDto>('/settings', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  async getByKey(key: string, branchId?: string): Promise<ApiSystemSettingDto> {
    const qs = branchId ? `?branchId=${branchId}` : '';
    return request<ApiSystemSettingDto>(`/settings/${encodeURIComponent(key)}${qs}`);
  },
};
