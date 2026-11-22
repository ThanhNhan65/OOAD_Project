import { request } from './apiClient';

export interface ApiReviewDto {
  id: string;
  memberId: string;
  memberName: string;
  trainerId?: string;
  trainerName?: string;
  bookingId?: string;
  classId?: string;
  className?: string;
  rating: number;
  comment?: string;
  targetType: 'trainer' | 'class' | string;
  createdAt: string;
}

export interface ApiCreateReviewPayload {
  trainerId?: string;
  bookingId?: string;
  classId?: string;
  rating: number;
  comment?: string;
}

export interface ApiTrainerReviewSummaryDto {
  trainerId: string;
  trainerName: string;
  averageRating: number;
  totalReviews: number;
  ratingDistribution: Record<number, number>;
  reviews: ApiReviewDto[];
}

export const reviewsApi = {
  getTrainerReviews: (trainerId: string) =>
    request<ApiTrainerReviewSummaryDto>(`/reviews/trainers/${trainerId}`),

  getMyReviews: () => request<ApiTrainerReviewSummaryDto>('/reviews/me'),

  submitReview: (payload: ApiCreateReviewPayload) =>
    request<ApiReviewDto>('/reviews', {
      method: 'POST',
      body: JSON.stringify(payload),
    }),
};
