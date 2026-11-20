import { request } from './apiClient';

export interface GymRuleApiDto {
  id: string;
  branchId?: string | null;
  branchName?: string | null;
  createdBy?: string | null;
  title: string;
  content: string;
  category: string;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CreateGymRulePayload {
  branchId?: string | null;
  title: string;
  content: string;
  category: string;
}

export interface UpdateGymRulePayload extends CreateGymRulePayload {
  isActive: boolean;
}

export const gymRulesApi = {
  getAll: (params?: { branchId?: string; category?: string; includeInactive?: boolean }) => {
    const qs = new URLSearchParams();
    if (params?.branchId) qs.set('branchId', params.branchId);
    if (params?.category) qs.set('category', params.category);
    if (params?.includeInactive) qs.set('includeInactive', 'true');
    const query = qs.toString();
    return request<GymRuleApiDto[]>(`/gym-rules${query ? `?${query}` : ''}`);
  },
  create: (data: CreateGymRulePayload) =>
    request<GymRuleApiDto>('/gym-rules', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  update: (id: string, data: UpdateGymRulePayload) =>
    request<GymRuleApiDto>(`/gym-rules/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  delete: (id: string) =>
    request<void>(`/gym-rules/${id}`, {
      method: 'DELETE',
    }),
};