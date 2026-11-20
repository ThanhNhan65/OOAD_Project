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

export function emitNotificationsChanged() {
  window.dispatchEvent(new Event(NOTIFICATIONS_CHANGED_EVENT));
}

export function mapNotificationType(type?: string): Notification['type'] {
  switch (type?.toLowerCase()) {
    case 'success':
      return 'success';
    case 'warning':
      return 'warning';
    case 'danger':
    case 'error':
      return 'danger';
    default:
      return 'info';
  }
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
  if (ref.includes('rule')) return '/rules';
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