import { request } from './apiClient';
import type { ApiWaitlistEntryDto } from './classesApi';
import type { PTBooking } from '../types';

export interface ApiBookingDto {
  id: string;
  memberId: string;
  membershipId: string;
  type: 'Class' | 'PersonalTrainer' | string;
  classSessionId?: string | null;
  availabilityId?: string | null;
  classId?: string | null;
  status: string;
  createdAt: string;
  cancelledAt?: string | null;
  title: string;
  category: string;
  color: string;
  branchId: string;
  branchName: string;
  room: string;
  trainerId: string;
  trainerName: string;
  startAt: string;
  endAt: string;
  canCancel: boolean;
  isCancelTimely: boolean;
  cancelDeadlineHours: number;
  canReview: boolean;
  isReviewed: boolean;
  checkedIn: boolean;
}

export interface RegisterClassResponseDto {
  outcome: 'CONFIRMED' | 'WAITLISTED' | string;
  message: string;
  booking?: ApiBookingDto | null;
  waitlistEntry?: ApiWaitlistEntryDto | null;
  waitlistPosition?: number | null;
}

export interface CancelBookingResultDto {
  bookingId: string;
  status: string;
  isTimely: boolean;
  quotaRefunded: boolean;
  promotedMemberName?: string | null;
  message: string;
}

export interface ApiMyScheduleSummaryDto {
  activePlanName?: string | null;
  membershipStatus?: string | null;
  priorityLevel: number;
  maxBookingsPerMonth: number;
  usedBookingsThisMonth: number;
  cancelDeadlineHours: number;
  waitlistCutoffHours: number;
  bookings: ApiBookingDto[];
  waitlistEntries: ApiWaitlistEntryDto[];
}

export interface CreateBookingReviewPayload {
  rating: number;
  comment?: string;
}

export interface BookingReviewDto {
  id: string;
  bookingId: string;
  rating: number;
  comment?: string | null;
  createdAt: string;
}

export const bookingsApi = {
  getMySchedule: () => request<ApiMyScheduleSummaryDto>('/bookings/me'),

  getClassBookings: () => request<ApiBookingDto[]>('/bookings/classes'),

  bookClass: (sessionId: string) =>
    request<RegisterClassResponseDto>('/bookings/classes', {
      method: 'POST',
      body: JSON.stringify({ classSessionId: sessionId }),
    }),

  cancelClassBooking: (bookingId: string) =>
    request<CancelBookingResultDto>(`/bookings/classes/${bookingId}/cancel`, {
      method: 'POST',
    }),

  cancelWaitlist: (waitlistEntryId: string) =>
    request<{ message: string }>(`/bookings/waitlist/${waitlistEntryId}/cancel`, {
      method: 'POST',
    }),

  submitReview: (bookingId: string, payload: CreateBookingReviewPayload) =>
    request<BookingReviewDto>(`/bookings/${bookingId}/review`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  getPTBookings: (memberId?: string, trainerId?: string) => {
    const params = new URLSearchParams();
    if (memberId) params.append('memberId', memberId);
    if (trainerId) params.append('trainerId', trainerId);
    return request<PTBooking[]>(`/bookings/pt?${params.toString()}`);
  },

  bookPT: (data: Partial<PTBooking>) =>
    request<PTBooking>('/bookings/pt', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  updatePTStatus: (bookingId: string, status: string) =>
    request<PTBooking>(`/bookings/pt/${bookingId}/status`, {
      method: 'PATCH',
      body: JSON.stringify({ status }),
    }),
};
