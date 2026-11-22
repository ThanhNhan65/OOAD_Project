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

export interface ApiNutritionPlanDto {
  id: string;
  memberId: string;
  memberName?: string;
  createdByTrainerId?: string;
  trainerName?: string;
  title: string;
  targetCalories: number;
  proteinGrams: number;
  carbsGrams: number;
  fatGrams: number;
  content?: string;
  createdAt: string;
}

export interface ApiCreateNutritionPlanPayload {
  memberId: string;
  title: string;
  targetCalories: number;
  proteinGrams: number;
  carbsGrams: number;
  fatGrams: number;
  content?: string;
}

export const bodyMeasurementsApi = {
  getMyMeasurements: () => request<ApiBodyMeasurementDto[]>('/body-measurements/me'),

  createMeasurement: (payload: ApiCreateBodyMeasurementPayload) =>
    request<ApiBodyMeasurementDto>('/body-measurements', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  getMetricTypes: () => request<ApiMetricTypeDto[]>('/body-measurements/metric-types'),

  getNutritionPlans: () => request<ApiNutritionPlanDto[]>('/body-measurements/nutrition-plans'),

  createNutritionPlan: (payload: ApiCreateNutritionPlanPayload) =>
    request<ApiNutritionPlanDto>('/body-measurements/nutrition-plans', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
};
