import { request } from './apiClient';
import type { Notification, Role } from '../types';

export interface NotificationApiDto {
  id: string;
  userId: string;
  type: string;
  title: string;
  content: string;
  isRead: boolean;
  createdAt: string;
  refType?: string | null;
  refId?: string | null;
}

export const NOTIFICATIONS_CHANGED_EVENT = 'ooad:notifications-changed';
export const NAV_COUNTS_CHANGED_EVENT = 'ooad:nav-counts-changed';

export function emitNotificationsChanged() {
  window.dispatchEvent(new Event(NOTIFICATIONS_CHANGED_EVENT));
}

export function emitNavCountsChanged() {
  window.dispatchEvent(new Event(NAV_COUNTS_CHANGED_EVENT));
}

export function emitAppDataChanged() {
  emitNotificationsChanged();
  emitNavCountsChanged();
}

export function mapNotificationType(type?: string): Notification['type'] {
  const t = type?.toLowerCase() ?? '';
  if (
    t.includes('success') ||
    t.includes('confirmed') ||
    t.includes('approved') ||
    t.includes('promoted')
  ) {
    return 'success';
  }
  if (
    t.includes('danger') ||
    t.includes('error') ||
    t.includes('rejected') ||
    t.includes('expired')
  ) {
    return 'danger';
  }
  if (
    t.includes('warning') ||
    t.includes('cancelled') ||
    t.includes('waitlist_joined') ||
    t.includes('freeze_requested')
  ) {
    return 'warning';
  }
  return 'info';
}

export function resolveNotificationLink(n: NotificationApiDto, role?: Role): string | null {
  const ref = n.refType?.toLowerCase();
  if (!ref) return null;
  if (ref.includes('equipment')) {
    if (role === 'admin') return '/admin/equipment';
    if (role === 'receptionist') return '/staff/equipment';
    return '/member/equipment';
  }
  if (ref.includes('freeze')) {
    return role === 'receptionist' ? '/staff/freeze' : '/member/memberships';
  }
  if (ref.includes('membership')) {
    return '/member/memberships';
  }
  if (ref.includes('card')) {
    return role === 'receptionist' ? '/staff/member-cards' : '/member/card';
  }
  if (ref.includes('booking') || ref.includes('waitlist') || ref.includes('class')) {
    if (role === 'trainer') return '/trainer/requests';
    return '/member/bookings';
  }
  return null;
}

export const notificationsApi = {
  getAll: (unreadOnly = false) =>
    request<NotificationApiDto[]>(`/notifications${unreadOnly ? '?unreadOnly=true' : ''}`),
  getUnreadCount: () => request<{ count: number }>('/notifications/unread-count'),
  markAsRead: async (id: string) => {
    await request<void>(`/notifications/${id}/read`, { method: 'PATCH' });
    emitNotificationsChanged();
  },
  markAllAsRead: async () => {
    await request<void>('/notifications/read-all', { method: 'PATCH' });
    emitNotificationsChanged();
  },
  deleteAll: async () => {
    await request<void>('/notifications', { method: 'DELETE' });
    emitNotificationsChanged();
  },
};