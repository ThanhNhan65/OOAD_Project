import { request } from './apiClient';

export interface BranchPresenceDto {
  branchId: string;
  branchName: string;
  operatingHours: string;
  presentCount: number;
}

export interface MonthlyRevenueDto {
  key: string;
  label: string;
  membership: number;
  pt: number;
  byBranch: Record<string, number>;
}

export interface MonthlyChurnDto {
  key: string;
  label: string;
  active: number;
  lost: number;
  newMembers: number;
  rate: number;
}

export interface PlanUsageDto {
  name: string;
  value: number;
  color: string;
}

export interface MonthlyVisitDto {
  label: string;
  count: number;
}

export interface HourlyCheckinDto {
  hour: string;
  count: number;
}

export interface ClassReportDto {
  classId: string;
  className: string;
  branchName: string;
  trainerName: string;
  sessions: number;
  fillRate: number;
  attendanceRate: number;
  waitlisted: number;
  rating: number;
  reviewCount: number;
}

export interface TrainerReportDto {
  trainerId: string;
  fullName: string;
  ptCompleted: number;
  ptNoShow: number;
  ptRejected: number;
  classSessions: number;
  rating: number;
  reviewCount: number;
}

export interface RecentPaymentDto {
  id: string;
  memberName: string;
  description: string;
  amount: number;
  createdAt: string;
}

export interface ReportOverviewDto {
  activeMembersNow: number;
  totalMembers: number;
  branchPresences: BranchPresenceDto[];
  revenue: MonthlyRevenueDto[];
  churn: MonthlyChurnDto[];
  planUsage: PlanUsageDto[];
  visits: MonthlyVisitDto[];
  hours: HourlyCheckinDto[];
  classes: ClassReportDto[];
  trainers: TrainerReportDto[];
  recentPayments: RecentPaymentDto[];
}

export const reportsApi = {
  getOverview: (branchId?: string, months = 6) => {
    const qs = new URLSearchParams();
    if (branchId && branchId !== 'all') qs.set('branchId', branchId);
    qs.set('months', String(months));
    return request<ReportOverviewDto>(`/reports/overview?${qs.toString()}`);
  },
};