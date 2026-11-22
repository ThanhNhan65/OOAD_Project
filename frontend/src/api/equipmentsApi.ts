import { request } from './apiClient';
import type { Equipment, EquipmentReport } from '../types';

export const equipmentsApi = {
  getAll: (branchId?: string) => request<Equipment[]>(`/equipments${branchId ? `?branchId=${branchId}` : ''}`),
  getById: (id: string) => request<Equipment>(`/equipments/${id}`),
  create: (data: Partial<Equipment>) => request<Equipment>('/equipments', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
  updateStatus: (id: string, status: string) => request<Equipment>(`/equipments/${id}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status }),
  }),
  getReports: (equipmentId?: string, branchId?: string) => {
    const params = new URLSearchParams();
    if (equipmentId) params.set('equipmentId', equipmentId);
    if (branchId) params.set('branchId', branchId);
    const qs = params.toString();
    return request<EquipmentReport[]>(`/equipments/reports${qs ? `?${qs}` : ''}`);
  },
  createReport: (data: Partial<EquipmentReport>) => request<EquipmentReport>('/equipments/reports', {
    method: 'POST',
    body: JSON.stringify(data),
  }),
  // Thêm mới hàm này ở đây
  updateReportStatus: (id: string, status: string, note: string) => request<EquipmentReport>(`/equipments/reports/${id}/status`, {
    method: 'PATCH',
    body: JSON.stringify({ status, note }),
  }),
};