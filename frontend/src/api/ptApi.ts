import { request } from './apiClient';

export interface ApiPtBalanceDto {
  total: number;
  freeSessions: number;
  packageSessions: number;
  activePlanName?: string;
  expiryDate?: string;
}

export interface ApiPtBookingDto {
  id: string;
  memberId: string;
  memberName: string;
  memberPhone?: string;
  trainerId: string;
  trainerName: string;
  trainerSpecialty?: string;
  branchId: string;
  branchName: string;
  availabilityId: string;
  startAt: string;
  endAt: string;
  status: 'PENDING' | 'CONFIRMED' | 'ATTENDED' | 'NOSHOW' | 'CANCELLED' | string;
  note?: string;
  sourceType: 'membership_free' | 'pt_package' | string;
  checkedIn: boolean;
  canCancel: boolean;
  canReview: boolean;
  isReviewed: boolean;
  createdAt: string;
}

export interface ApiBookPtPayload {
  availabilityId: string;
  note?: string;
}

export interface ApiTrainerPtRequestDto {
  id: string;
  memberId: string;
  memberName: string;
  memberPhone?: string;
  branchId: string;
  branchName: string;
  startAt: string;
  endAt: string;
  status: 'PENDING' | 'CONFIRMED' | 'ATTENDED' | 'NOSHOW' | 'CANCELLED' | string;
  note?: string;
  sourceType: 'membership_free' | 'pt_package' | string;
  checkedIn: boolean;
  createdAt: string;
}

export const ptApi = {
  getPtBalance: () => request<ApiPtBalanceDto>('/trainers/pt-balance'),

  bookPt: (payload: ApiBookPtPayload) =>
    request<ApiPtBookingDto>('/trainers/pt-bookings', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  getMyPtBookings: () => request<ApiPtBookingDto[]>('/trainers/pt-bookings/me'),

  cancelPtBooking: (id: string, reason?: string) => {
    const qs = reason ? `?reason=${encodeURIComponent(reason)}` : '';
    return request<{ message: string }>(`/trainers/pt-bookings/${id}/cancel${qs}`, {
      method: 'POST',
    });
  },

  getTrainerPtRequests: () => request<ApiTrainerPtRequestDto[]>('/trainers/me/pt-requests'),

  respondPtRequest: (id: string, accept: boolean, reason?: string) =>
    request<{ message: string }>(`/trainers/pt-requests/${id}/respond`, {
      method: 'POST',
      body: JSON.stringify({ accept, reason }),
    }),

  completePtSession: (id: string, attended: boolean) =>
    request<{ message: string }>(`/trainers/pt-requests/${id}/complete`, {
      method: 'POST',
      body: JSON.stringify({ attended }),
    }),

  cancelPtSessionByTrainer: (id: string, reason: string) =>
    request<{ message: string }>(`/trainers/pt-requests/${id}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ accept: false, reason }),
    }),
};
