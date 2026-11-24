import { request } from './apiClient';

export interface ApiGymClassDto {
  id: string;
  branchId: string;
  branchName: string;
  trainerId: string;
  trainerUserId: string;
  trainerName: string;
  trainerSpecialty?: string;
  name: string;
  room: string;
  capacity: number;
  dayOfWeek: number;
  daysOfWeek: number[];
  startTime: string;
  endTime: string;
  durationMinutes: number;
  category: string;
  color: string;
  upcomingSessionCount: number;
  active: boolean;
  averageRating: number;
  reviewCount: number;
}

export interface CreateGymClassPayload {
  branchId: string;
  trainerId: string;
  name: string;
  room: string;
  capacity: number;
  dayOfWeek?: number;
  daysOfWeek?: number[];
  startTime: string;
  endTime?: string;
  durationMinutes?: number;
  generateWeeks?: number;
}

export interface UpdateGymClassPayload extends CreateGymClassPayload {
  regenerateFutureSessions?: boolean;
}

export interface GenerateSessionsPayload {
  weeks?: number;
  daysOfWeek?: number[];
}

export interface ApiClassSessionDto {
  id: string;
  classId: string;
  className: string;
  category: string;
  color: string;
  branchId: string;
  branchName: string;
  trainerId: string;
  trainerUserId: string;
  trainerName: string;
  room: string;
  capacity: number;
  sessionDate: string;
  startAt: string;
  endAt: string;
  durationMinutes: number;
  status: string;
  bookedCount: number;
  availableSlots: number;
  waitlistCount: number;
  attendanceConfirmed: boolean;
  attendedCount: number;
  canJoinWaitlist: boolean;
  hoursUntilStart: number;
  averageRating: number;
  reviewCount: number;
  myBookingId?: string | null;
  myBookingStatus?: string | null;
  myWaitlistEntryId?: string | null;
  myWaitlistPosition?: number | null;
}

export interface ApiSessionRosterItemDto {
  bookingId: string;
  memberId: string;
  userId: string;
  memberName: string;
  memberEmail: string;
  memberPhone?: string;
  cardCode?: string;
  bookingStatus: string;
  hasCheckedInAtBranch: boolean;
  checkInAt?: string | null;
  bookedAt: string;
}

export interface ApiWaitlistEntryDto {
  id: string;
  classSessionId: string;
  className: string;
  category: string;
  color: string;
  branchId: string;
  branchName: string;
  room: string;
  trainerName: string;
  startAt: string;
  endAt: string;
  memberId: string;
  userId: string;
  memberName: string;
  memberEmail: string;
  planName?: string;
  priorityLevel: number;
  position: number;
  registeredAt: string;
  status: string;
}

export interface ApiClassSessionDetailDto extends ApiClassSessionDto {
  roster: ApiSessionRosterItemDto[];
  waitlist: ApiWaitlistEntryDto[];
  cancelDeadlineHours: number;
  waitlistCutoffHours: number;
}

export interface RescheduleSessionPayload {
  sessionDate: string;
  startTime: string;
  endTime?: string;
  durationMinutes?: number;
  room?: string;
  trainerId?: string;
}

export interface ConfirmAttendancePayload {
  attendedBookingIds?: string[];
  attendedMemberIds?: string[];
}

export interface ConfirmAttendanceResultDto {
  sessionId: string;
  attendedCount: number;
  noShowCount: number;
  rejectedNotCheckedInCount: number;
  rejectedMemberNames: string[];
  message: string;
}

export interface WaitlistCutoffResultDto {
  expiredCount: number;
  message: string;
}

export const classesApi = {
  getAll: (branchId?: string, trainerId?: string) => {
    const params = new URLSearchParams();
    if (branchId) params.set('branchId', branchId);
    if (trainerId) params.set('trainerId', trainerId);
    const qs = params.toString();
    return request<ApiGymClassDto[]>(`/classes${qs ? `?${qs}` : ''}`);
  },

  getById: (id: string) => request<ApiGymClassDto>(`/classes/${id}`),

  create: (data: CreateGymClassPayload) =>
    request<ApiGymClassDto>('/classes', {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  update: (id: string, data: UpdateGymClassPayload) =>
    request<ApiGymClassDto>(`/classes/${id}`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  remove: (id: string) =>
    request<void>(`/classes/${id}`, {
      method: 'DELETE',
    }),

  generateSessions: (id: string, data?: GenerateSessionsPayload) =>
    request<ApiClassSessionDto[]>(`/classes/${id}/generate-sessions`, {
      method: 'POST',
      body: JSON.stringify(data ?? { weeks: 2 }),
    }),

  getSessions: (params?: {
    branchId?: string;
    classId?: string;
    trainerId?: string;
    from?: string;
    to?: string;
    status?: string;
  }) => {
    const qs = new URLSearchParams();
    if (params?.branchId) qs.set('branchId', params.branchId);
    if (params?.classId) qs.set('classId', params.classId);
    if (params?.trainerId) qs.set('trainerId', params.trainerId);
    if (params?.from) qs.set('from', params.from);
    if (params?.to) qs.set('to', params.to);
    if (params?.status) qs.set('status', params.status);
    const queryStr = qs.toString();
    return request<ApiClassSessionDto[]>(`/classes/sessions${queryStr ? `?${queryStr}` : ''}`);
  },

  getSessionDetail: (sessionId: string) =>
    request<ApiClassSessionDetailDto>(`/classes/sessions/${sessionId}`),

  getTrainerMySessions: (filter?: 'all' | 'upcoming' | 'tomark' | 'done') => {
    const qs = filter ? `?filter=${encodeURIComponent(filter)}` : '';
    return request<ApiClassSessionDto[]>(`/classes/trainer/my-sessions${qs}`);
  },

  rescheduleSession: (sessionId: string, data: RescheduleSessionPayload) =>
    request<ApiClassSessionDto>(`/classes/sessions/${sessionId}/reschedule`, {
      method: 'PUT',
      body: JSON.stringify(data),
    }),

  cancelSession: (sessionId: string, reason?: string) =>
    request<ApiClassSessionDto>(`/classes/sessions/${sessionId}/cancel`, {
      method: 'POST',
      body: JSON.stringify({ reason }),
    }),

  confirmAttendance: (sessionId: string, data: ConfirmAttendancePayload) =>
    request<ConfirmAttendanceResultDto>(`/classes/sessions/${sessionId}/attendance`, {
      method: 'POST',
      body: JSON.stringify(data),
    }),

  processWaitlistCutoff: () =>
    request<WaitlistCutoffResultDto>('/classes/sessions/process-waitlist-cutoff', {
      method: 'POST',
    }),
};
