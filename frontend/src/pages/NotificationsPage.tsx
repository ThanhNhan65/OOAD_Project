import { useState, useEffect, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { Bell, CheckCheck, Trash2 } from 'lucide-react';
import { useApp } from '../store/AppContext';
import { PageHead, Tabs, Empty } from '../components/ui';
import { ntfStyle } from '../components/Layout';
import { timeAgo, fmtDateTime } from '../utils';
import {
  notificationsApi,
  mapNotificationType,
  resolveNotificationLink,
  NOTIFICATIONS_CHANGED_EVENT,
  type NotificationApiDto,
} from '../api/notificationsApi';

export function NotificationsPage() {
  const { user } = useApp();
  const nav = useNavigate();
  const [filter, setFilter] = useState<'all' | 'unread'>('all');
  const [list, setList] = useState<NotificationApiDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [err, setErr] = useState('');

  const loadData = useCallback(async () => {
    setLoading(true);
    setErr('');
    try {
      const data = await notificationsApi.getAll();
      setList(data);
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Không thể tải thông báo');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
    window.addEventListener(NOTIFICATIONS_CHANGED_EVENT, loadData);
    return () => window.removeEventListener(NOTIFICATIONS_CHANGED_EVENT, loadData);
  }, [loadData]);

  const notifications = filter === 'unread' ? list.filter((n) => !n.isRead) : list;
  const unreadCount = list.filter((n) => !n.isRead).length;

  const markAllRead = async () => {
    try {
      await notificationsApi.markAllAsRead();
      setList((prev) => prev.map((n) => ({ ...n, isRead: true })));
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Cập nhật thất bại');
    }
  };

  const clearAll = async () => {
    try {
      await notificationsApi.deleteAll();
      setList([]);
    } catch (e) {
      setErr(e instanceof Error ? e.message : 'Xóa thông báo thất bại');
    }
  };

  const handleClick = async (n: NotificationApiDto) => {
    if (!n.isRead) {
      try {
        await notificationsApi.markAsRead(n.id);
        setList((prev) => prev.map((x) => (x.id === n.id ? { ...x, isRead: true } : x)));
      } catch {
        // ignore
      }
    }
    const link = resolveNotificationLink(n, user?.role);
    if (link) nav(link);
  };

  return (
    <>
      <PageHead
        title="Thông báo"
        desc="Nhắc lịch tập, biến động gói tập, kết quả duyệt freeze và các cập nhật quan trọng."
        actions={
          <div className="row wrap" style={{ gap: 8 }}>
            <button className="btn sm" onClick={markAllRead} disabled={unreadCount === 0} id="btn-mark-all-read">
              <CheckCheck size={16} /> Đánh dấu đã đọc tất cả
            </button>
            <button className="btn sm danger" onClick={clearAll} disabled={list.length === 0} id="btn-clear-all-ntf">
              <Trash2 size={16} /> Xóa tất cả
            </button>
          </div>
        }
      />

      {err && <div className="card mb16" style={{ borderColor: 'var(--red)', color: 'var(--red)', padding: 12 }}>{err}</div>}

      <Tabs
        value={filter}
        onChange={setFilter}
        tabs={[
          { id: 'all', label: 'Tất cả', count: list.length },
          { id: 'unread', label: 'Chưa đọc', count: unreadCount },
        ]}
      />

      {loading ? (
        <div className="card" style={{ padding: 24 }}>Đang tải thông báo...</div>
      ) : notifications.length === 0 ? (
        <div className="card">
          <Empty text="Không có thông báo nào" icon={<Bell size={40} />} />
        </div>
      ) : (
        <div className="col" style={{ gap: 8 }}>
          {notifications.map((n) => {
            const style = ntfStyle[mapNotificationType(n.type)];
            const link = resolveNotificationLink(n, user?.role);
            return (
              <div
                key={n.id}
                className={`list-item ${!n.isRead ? 'card' : ''}`}
                style={{
                  cursor: link || !n.isRead ? 'pointer' : 'default',
                  borderLeft: `4px solid ${style.c}`,
                  background: !n.isRead ? 'rgba(124, 92, 255, 0.08)' : undefined,
                }}
                onClick={() => handleClick(n)}
              >
                <div
                  className="ntf-icon"
                  style={{
                    background: `${style.c}22`,
                    color: style.c,
                    width: 40,
                    height: 40,
                    borderRadius: 12,
                    display: 'grid',
                    placeItems: 'center',
                    flexShrink: 0,
                  }}
                >
                  {style.i}
                </div>
                <div className="grow" style={{ minWidth: 0 }}>
                  <div className="row wrap" style={{ gap: 8 }}>
                    <b style={{ fontSize: 14 }}>{n.title}</b>
                    {!n.isRead && <span className="badge b-purple plain">Mới</span>}
                  </div>
                  <div className="muted mt8" style={{ marginTop: 4 }}>{n.content}</div>
                  <div className="small dim mt8" style={{ marginTop: 6 }}>
                    {fmtDateTime(n.createdAt)} · {timeAgo(n.createdAt)}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}