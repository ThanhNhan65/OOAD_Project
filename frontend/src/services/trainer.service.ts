import { request } from '../api/apiClient';

export interface TrainerAvailabilityItem {
  id: string;
  trainerId: string;
  startAt: string;
  endAt: string;
  isBooked: boolean;
}

export interface TrainerItem {
  id: string;
  userId: string;
  branchId: string;
  fullName: string;
  email: string;
  phone?: string;
  branchName?: string;
  specialty?: string;
  experienceYears: number;
  bio?: string;
  averageRating: number;
  reviewCount: number;
  availabilities?: TrainerAvailabilityItem[];
}

export interface UpdateTrainerProfilePayload {
  specialty?: string;
  experienceYears?: number;
  bio?: string;
  phone?: string;
  branchId?: string;
}

export interface AvailabilityPayload {
  startAt: string;
  endAt: string;
}

export interface TrainerScheduleItem {
  id: string;
  type: 'CLASS' | 'PT';
  title: string;
  startAt: string;
  endAt: string;
  room?: string;
  clientName?: string;
  clientPhone?: string;
  status?: string;
}

export const trainerService = {
  // GET /api/trainers
  async getAllTrainers(branchId?: string): Promise<TrainerItem[]> {
    const qs = branchId ? `?branchId=${encodeURIComponent(branchId)}` : '';
    return request<TrainerItem[]>(`/trainers${qs}`);
  },

  // GET /api/trainers/{id}
  async getTrainerById(id: string): Promise<TrainerItem> {
    return request<TrainerItem>(`/trainers/${id}`);
  },

  // GET /api/trainers/me
  async getMyProfile(): Promise<TrainerItem> {
    return request<TrainerItem>('/trainers/me');
  },

  // PUT /api/trainers/me
  async updateMyProfile(payload: UpdateTrainerProfilePayload): Promise<TrainerItem> {
    return request<TrainerItem>('/trainers/me', {
      method: 'PUT',
      body: JSON.stringify(payload),
    });
  },

  // GET /api/trainers/{id}/availability
  async getAvailability(id: string, from?: string, to?: string): Promise<TrainerAvailabilityItem[]> {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const qs = params.toString() ? `?${params.toString()}` : '';
    return request<TrainerAvailabilityItem[]>(`/trainers/${id}/availability${qs}`);
  },

  // POST /api/trainers/availability
  async addAvailability(payload: AvailabilityPayload): Promise<TrainerAvailabilityItem> {
    return request<TrainerAvailabilityItem>('/trainers/availability', {
      method: 'POST',
      body: JSON.stringify(payload),
    });
  },

  // DELETE /api/trainers/availability/{id}
  async deleteAvailability(id: string): Promise<void> {
    await request<void>(`/trainers/availability/${id}`, {
      method: 'DELETE',
    });
  },

  // GET /api/trainers/my-schedule
  async getMySchedule(from?: string, to?: string): Promise<TrainerScheduleItem[]> {
    const params = new URLSearchParams();
    if (from) params.set('from', from);
    if (to) params.set('to', to);
    const qs = params.toString() ? `?${params.toString()}` : '';
    return request<TrainerScheduleItem[]>(`/trainers/my-schedule${qs}`);
  },
};
