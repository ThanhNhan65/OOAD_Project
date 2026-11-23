import { request } from './apiClient';

export interface ApiMembershipPlanDto {
  id: string;
  name: string;
  durationDays: number;
  durationMonths: number;
  price: number;
  priorityLevel: number;
  maxBookingsPerMonth: number;
  maxFreezeTimes: number;
  maxFreezeDays: number;
  isAllBranches: boolean;
  planType: 'MEMBERSHIP' | 'PT' | string;
  ptSessions: number;
  branchIds: string[];
  branchNames: string[];
  isActive: boolean;
}

export interface CreateUpdatePlanPayload {
  name: string;
  durationDays: number;
  price: number;
  priorityLevel: number;
  maxBookingsPerMonth: number;
  maxFreezeTimes: number;
  maxFreezeDays: number;
  isAllBranches: boolean;
  planType: 'MEMBERSHIP' | 'PT';
  ptSessions: number;
  branchIds?: string[];
}

export interface ApiMembershipDto {
  id: string;
  memberId: string;
  memberName: string;
  planId: string;
  planName: string;
  planType: 'MEMBERSHIP' | 'PT' | string;
  price: number;
  priorityLevel: number;
  durationDays: number;
  branchId?: string | null;
  branchName?: string | null;
  isAllBranches: boolean;
  branchIds: string[];
  renewedFromId?: string | null;
  isRenewal: boolean;
  startDate: string;
  endDate: string;
  remainingSessions: number;
  totalPtSessions: number;
  maxFreezeTimes: number;
  maxFreezeDays: number;
  freezeCountUsed: number;
  freezeDaysUsed: number;
  status: 'Active' | 'Pending' | 'Frozen' | 'Expired' | 'Cancelled' | string;
  payStatus: 'paid' | 'pending_payment' | string;
  createdAt: string;
}

export interface ApiPaymentDto {
  id: string;
  membershipId: string;
  memberId: string;
  memberName: string;
  planId: string;
  planName: string;
  planType: 'MEMBERSHIP' | 'PT' | string;
  branchId?: string | null;
  branchName?: string | null;
  description: string;
  amount: number;
  method: string;
  status: 'Completed' | 'Failed' | 'Pending' | string;
  gatewayTxnCode?: string | null;
  createdAt: string;
  paidAt?: string | null;
}

export interface ApiFreezeRequestDto {
  id: string;
  membershipId: string;
  memberId: string;
  memberName: string;
  memberPhone?: string | null;
  planId: string;
  planName: string;
  planEndDate: string;
  maxFreezeTimes: number;
  maxFreezeDays: number;
  freezeCountUsed: number;
  freezeDaysUsed: number;
  affectedBookingsCount: number;
  fromDate: string;
  toDate: string;
  days: number;
  reason: string;
  status: 'Pending' | 'Approved' | 'Rejected' | string;
  handledBy?: string | null;
  handledByName?: string | null;
  createdAt: string;
  handledAt?: string | null;
}

export interface PurchaseMembershipPayload {
  planId: string;
  branchId?: string;
  paymentMethod: string;
  simulateSuccess: boolean;
}

export interface PurchaseResultDto {
  membership: ApiMembershipDto;
  payment: ApiPaymentDto;
  success: boolean;
  message: string;
}

export const membershipPlansApi = {
  // Danh mục gói tập (MembershipPlansController)
  getAll: (planType?: 'MEMBERSHIP' | 'PT' | 'all') => {
    const qs = planType && planType !== 'all' ? `?planType=${encodeURIComponent(planType)}` : '';
    return request<ApiMembershipPlanDto[]>(`/membership-plans${qs}`);
  },
  getPTPlans: () => request<ApiMembershipPlanDto[]>('/membership-plans/pt'),
  getById: (id: string) => request<ApiMembershipPlanDto>(`/membership-plans/${id}`),
  create: (payload: CreateUpdatePlanPayload) =>
    request<ApiMembershipPlanDto>('/membership-plans', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  update: (id: string, payload: CreateUpdatePlanPayload) =>
    request<ApiMembershipPlanDto>(`/membership-plans/${id}`, {
      method: 'PUT',
      body: JSON.stringify(payload),
    }),
  remove: (id: string) =>
    request<void>(`/membership-plans/${id}`, {
      method: 'DELETE',
    }),

  // Gói tập của Hội viên & Thanh toán (MembershipsController & PaymentsController)
  getMyMemberships: () => request<ApiMembershipDto[]>('/memberships/me'),
  purchase: (payload: PurchaseMembershipPayload) =>
    request<PurchaseResultDto>('/memberships/purchase', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  retryPayment: (
    membershipId: string,
    payload: { branchId?: string; paymentMethod: string; simulateSuccess: boolean },
  ) =>
    request<PurchaseResultDto>(`/memberships/${membershipId}/retry-payment`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),

  getMyPayments: () => request<ApiPaymentDto[]>('/payments/me'),
  getAllPayments: (branchId?: string) => {
    const qs = branchId && branchId !== 'all' ? `?branchId=${encodeURIComponent(branchId)}` : '';
    return request<ApiPaymentDto[]>(`/payments${qs}`);
  },

  // Yêu cầu tạm ngưng gói (FreezeRequestsController)
  getMyFreezeRequests: () => request<ApiFreezeRequestDto[]>('/freeze-requests/me'),
  getAllFreezeRequests: (status?: string) => {
    const qs = status && status !== 'all' ? `?status=${encodeURIComponent(status)}` : '';
    return request<ApiFreezeRequestDto[]>(`/freeze-requests${qs}`);
  },
  createFreezeRequest: (payload: { membershipId: string; fromDate: string; days: number; reason: string }) =>
    request<ApiFreezeRequestDto>('/freeze-requests', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
  handleFreezeRequest: (id: string, payload: { approve: boolean; note?: string }) =>
    request<ApiFreezeRequestDto>(`/freeze-requests/${id}/handle`, {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
};
