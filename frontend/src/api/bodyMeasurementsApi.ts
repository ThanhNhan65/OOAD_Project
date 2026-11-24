import { request } from './apiClient';

export interface ApiBodyMeasurementDto {
  id: string;
  memberId: string;
  measuredAt: string;
  isInitial: boolean;
  note?: string;
  weight: number;
  height: number;
  bmi: number;
  bodyFat: number;
  muscleMass: number;
  waist: number;
  chest: number;
  hips: number;
  metrics: Record<string, number>;
}

export interface ApiCreateBodyMeasurementPayload {
  weight: number;
  height?: number;
  bmi?: number;
  bodyFat?: number;
  muscleMass?: number;
  waist?: number;
  chest?: number;
  hips?: number;
  note?: string;
  measuredAt?: string;
}

export interface ApiMetricTypeDto {
  id: string;
  code: string;
  name: string;
  unit: string;
  isActive: boolean;
}

export const bodyMeasurementsApi = {
  getMyMeasurements: () => request<ApiBodyMeasurementDto[]>('/body-measurements/me'),

  createMeasurement: (payload: ApiCreateBodyMeasurementPayload) =>
    request<ApiBodyMeasurementDto>('/body-measurements', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  deleteMeasurement: (id: string) =>
    request<void>(`/body-measurements/${id}`, {
      method: 'DELETE',
    }),

  getMetricTypes: () => request<ApiMetricTypeDto[]>('/body-measurements/metric-types'),
};
