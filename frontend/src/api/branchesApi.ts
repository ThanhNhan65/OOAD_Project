import { request } from './apiClient';

export interface BranchApiDto {
  id: string;
  name: string;
  district: string;
  address: string;
  phoneNumber: string;
  operatingHours: string;
  isActive: boolean;
}

export interface CreateBranchPayload {
  name: string;
  district: string;
  address: string;
  phoneNumber: string;
  operatingHours: string;
}

export interface UpdateBranchPayload extends CreateBranchPayload {
  isActive: boolean;
}

export const branchesApi = {
  getAll: () => request<BranchApiDto[]>('/branches'),
  getById: (id: string) => request<BranchApiDto>(`/branches/${id}`),
  create: (data: CreateBranchPayload) =>
    request<BranchApiDto>('/branches', {
      method: 'POST',
      body: JSON.stringify(data),
    }),
  update: (id: string, data: UpdateBranchPayload) =>
    request<BranchApiDto>(`/branches/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),
  delete: (id: string) =>
    request<void>(`/branches/${id}`, {
      method: 'DELETE',
    }),
};