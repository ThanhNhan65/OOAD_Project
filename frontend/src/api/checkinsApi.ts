import { request } from './apiClient';
import { userService, type ApiMemberDto } from './user.service';

export interface ApiCheckinDto {
  id: string;
  memberId: string;
  userId: string;
  memberName: string;
  memberEmail: string;
  memberPhone?: string | null;
  cardCode?: string | null;
  branchId: string;
  branchName: string;
  cardId?: string | null;
  bookingId?: string | null;
  attendedClassName?: string | null;
  recordedBy?: string | null;
  recordedByName?: string | null;
  method: 'Card' | 'QR' | 'Manual' | 'Facial' | string;
  note?: string | null;
  checkInAt: string;
  checkOutAt?: string | null;
  isAutoCheckout: boolean;
  durationMinutes?: number | null;
}

export interface ApiCheckinOperationResultDto {
  allowed: boolean;
  action: 'CHECK_IN' | 'CHECK_OUT' | 'DENIED' | string;
  message: string;
  denyCode?: string | null;
  checkin?: ApiCheckinDto | null;
  memberId?: string | null;
  memberName?: string | null;
  cardCode?: string | null;
  cardStatus?: string | null;
  planName?: string | null;
  membershipStatus?: string | null;
  planEndDate?: string | null;
  autoAttendedBooking: boolean;
  attendedBookingSummary?: string | null;
}

export interface ApiStaffDashboardDto {
  branchId: string;
  branchName: string;
  operatingHours: string;
  presentCount: number;
  todayCheckinCount: number;
  todayManualCheckinCount: number;
  pendingFreezeCount: number;
  openEquipmentIssueCount: number;
  presentMembers: ApiCheckinDto[];
}

export const checkinsApi = {
  getStaffDashboard: (branchId?: string) => {
    const qs = branchId ? `?branchId=${encodeURIComponent(branchId)}` : '';
    return request<ApiStaffDashboardDto>(`/staff/dashboard${qs}`);
  },

  checkInByCard: (cardCode: string, branchId: string, method: 'QR' | 'Card' = 'QR') =>
    request<ApiCheckinOperationResultDto>('/checkins/card', {
      method: 'POST',
      body: JSON.stringify({ cardCode, branchId, method }),
    }),

  checkOutByCard: (cardCode: string) =>
    request<ApiCheckinOperationResultDto>('/checkins/card/checkout', {
      method: 'POST',
      body: JSON.stringify({ cardCode }),
    }),

  checkInManual: (memberId: string, branchId: string | undefined, reason: string) =>
    request<ApiCheckinOperationResultDto>('/checkins/manual', {
      method: 'POST',
      body: JSON.stringify({ memberId, branchId: branchId || null, reason }),
    }),

  checkOutManual: (memberId: string) =>
    request<ApiCheckinOperationResultDto>('/checkins/manual/checkout', {
      method: 'POST',
      body: JSON.stringify({ memberId }),
    }),

  checkOutById: (checkInId: string) =>
    request<ApiCheckinOperationResultDto>(`/checkins/${checkInId}/checkout`, {
      method: 'POST',
    }),

  getActive: (branchId?: string) => {
    const qs = branchId && branchId !== 'all' ? `?branchId=${encodeURIComponent(branchId)}` : '';
    return request<ApiCheckinDto[]>(`/checkins/active${qs}`);
  },

  getHistory: (params?: { branchId?: string; method?: string; date?: string; search?: string }) => {
    const qs = new URLSearchParams();
    if (params?.branchId && params.branchId !== 'all') qs.set('branchId', params.branchId);
    if (params?.method && params.method !== 'all') qs.set('method', params.method);
    if (params?.date) qs.set('date', params.date);
    if (params?.search) qs.set('search', params.search);
    const s = qs.toString();
    return request<ApiCheckinDto[]>(`/checkins${s ? `?${s}` : ''}`);
  },

  getMyHistory: () => request<ApiCheckinDto[]>('/checkins/me'),

  runAutoCheckout: () =>
    request<{ closedCount: number; message: string }>('/checkins/auto-checkout', {
      method: 'POST',
    }),

  searchMembers: (search?: string): Promise<ApiMemberDto[]> => userService.searchMembers(search),
};
