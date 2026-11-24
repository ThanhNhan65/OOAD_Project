import { useEffect, useState, useCallback, useMemo } from 'react';
import { Link } from 'react-router-dom';
import { CalendarDays, Inbox, Star, Clock, ClipboardCheck, ArrowRight, UserCheck, RefreshCw } from 'lucide-react';
import { useApp } from '../../../store/AppContext';
import { Badge, Empty, StatCard, Stars } from '../../../components/ui';
import { addDays, todayStr, fmtDate, fmtTime, fmtDateTime, fmtWeekday } from '../../../utils';
import { ptApi, type ApiTrainerPtRequestDto } from '../../../api/ptApi';
import { reviewsApi, type ApiTrainerReviewSummaryDto } from '../../../api/reviewsApi';
import { classesApi, type ApiClassSessionDto } from '../../../api/classesApi';
import { isApiNetworkError } from '../../../api/apiClient';

export function TrainerHomeScreen() {
  const { user, toast } = useApp();
  const [loading, setLoading] = useState<boolean>(false);
  const [apiRequests, setApiRequests] = useState<ApiTrainerPtRequestDto[]>([]);
  const [apiReviews, setApiReviews] = useState<ApiTrainerReviewSummaryDto | null>(null);
  const [apiSessions, setApiSessions] = useState<ApiClassSessionDto[]>([]);

  const fetchStats = useCallback(async () => {
    setLoading(true);
    try {
      const [reqs, revs, sess] = await Promise.allSettled([
        ptApi.getTrainerPtRequests(),
        reviewsApi.getMyReviews(),
        classesApi.getTrainerMySessions('all'),
      ]);
      if (reqs.status === 'fulfilled') setApiRequests(reqs.value);
      if (revs.status === 'fulfilled') setApiReviews(revs.value);
      if (sess.status === 'fulfilled') setApiSessions(sess.value);
    } catch (err) {
      if (!isApiNetworkError(err)) {
        toast('Không thể tải thống kê từ máy chủ', 'error');
      }
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    fetchStats();
  }, [fetchStats]);

  const today = todayStr();

  const pendingCount = useMemo(
    () => apiRequests.filter((r) => r.status.toLowerCase() === 'pending').length,
    [apiRequests],
  );

  const ptDoneCount = useMemo(
    () =>
      apiRequests.filter((r) => {
        const st = r.status.toLowerCase();
        return st === 'attended' || st === 'completed';
      }).length,
    [apiRequests],
  );

  const upcomingSessions = useMemo(() => {
    const nowMs = Date.now();
    return apiSessions
      .filter((x) => x.status.toLowerCase() !== 'cancelled' && new Date(x.startAt).getTime() > nowMs)
      .sort((a, b) => a.startAt.localeCompare(b.startAt));
  }, [apiSessions]);

  const toMark = useMemo(() => {
    const nowMs = Date.now();
    return apiSessions
      .filter(
        (x) =>
          x.status.toLowerCase() !== 'cancelled' &&
          new Date(x.startAt).getTime() <= nowMs &&
          !x.attendanceConfirmed &&
          x.bookedCount > 0,
      )
      .sort((a, b) => b.startAt.localeCompare(a.startAt));
  }, [apiSessions]);

  const avgRating = apiReviews ? apiReviews.averageRating : 0;
  const reviewCount = apiReviews ? apiReviews.totalReviews : 0;

  const agenda = useMemo(() => {
    const limitDate = addDays(today, 2);
    const ptLimitDate = addDays(today, 3);
    const nowMs = Date.now();

    const classAgenda = upcomingSessions
      .filter((x) => x.startAt.slice(0, 10) <= limitDate)
      .map((x) => ({
        at: x.startAt,
        title: x.className,
        sub: `${x.room} · ${x.bookedCount}/${x.capacity} học viên`,
        type: 'Lớp nhóm',
        color: x.color || '#7c5cff',
      }));

    const ptAgenda = apiRequests
      .filter(
        (r) =>
          r.status.toLowerCase() === 'confirmed' &&
          new Date(r.startAt).getTime() > nowMs &&
          r.startAt.slice(0, 10) <= ptLimitDate,
      )
      .map((b) => ({
        at: b.startAt,
        title: `PT · ${b.memberName}`,
        sub: b.note || b.branchName,
        type: '1-kèm-1',
        color: '#00d1b2',
      }));

    return [...classAgenda, ...ptAgenda].sort((a, b) => a.at.localeCompare(b.at));
  }, [upcomingSessions, apiRequests, today]);

  return (
    <>
      <div className="card hero-card mb16" style={{ padding: 28 }}>
        <div className="row between wrap">
          <div>
            <div className="muted">Chào HLV,</div>
            <h1 style={{ fontSize: 30 }}>{user!.fullName} 💪</h1>
            <div className="row wrap mt8">
              {user!.trainer?.specialties.map((x) => (
                <Badge key={x} color="purple" plain>{x}</Badge>
              ))}
            </div>
          </div>
          <div className="row">
            <button className="btn ghost sm" onClick={fetchStats} disabled={loading}>
              <RefreshCw size={14} className={loading ? 'spin' : ''} />
            </button>
            <Link to="/trainer/availability" className="btn">
              <Clock size={16} /> Cập nhật lịch rảnh
            </Link>
            <Link to="/trainer/requests" className="btn primary">
              <Inbox size={16} /> Yêu cầu ({pendingCount})
            </Link>
          </div>
        </div>
      </div>
      <div className="grid g4 mb16">
        <StatCard icon={<Inbox size={22} />} label="Yêu cầu chờ xác nhận" value={pendingCount} color="#ffb020" />
        <StatCard icon={<CalendarDays size={22} />} label="Buổi lớp sắp tới" value={upcomingSessions.length} color="#7c5cff" />
        <StatCard icon={<UserCheck size={22} />} label="Buổi PT đã dạy" value={ptDoneCount} color="#00d1b2" />
        <StatCard icon={<Star size={22} />} label={`Đánh giá TB (${reviewCount})`} value={avgRating ? avgRating.toFixed(2) : '—'} color="#ff5c8a" />
      </div>
      <div className="grid g2">
        <div className="card">
          <div className="card-head"><h2><CalendarDays size={18} /> Lịch dạy sắp tới</h2></div>
          {agenda.length === 0 ? <Empty text="Không có lịch trong vài ngày tới" /> : (
            <div className="col">
              {agenda.slice(0, 7).map((a, i) => (
                <div className="list-item" key={i}>
                  <div className="date-box" style={{ background: a.color + '22', borderColor: a.color + '66' }}>
                    <div className="d">{new Date(a.at).getDate()}</div>
                    <div className="m">{fmtTime(a.at)}</div>
                  </div>
                  <div className="grow">
                    <b>{a.title}</b>
                    <div className="small muted">{fmtWeekday(a.at)} · {a.sub}</div>
                  </div>
                  <Badge color={a.type === 'Lớp nhóm' ? 'purple' : 'blue'} plain>{a.type}</Badge>
                </div>
              ))}
            </div>
          )}
        </div>
        <div className="card">
          <div className="card-head">
            <h2><ClipboardCheck size={18} /> Cần điểm danh</h2>
            <Link to="/trainer/classes" className="btn ghost sm">Mở <ArrowRight size={14} /></Link>
          </div>
          {toMark.length === 0 ? <Empty text="Đã điểm danh đầy đủ 🎉" /> : (
            <div className="col">
              {toMark.slice(0, 5).map((x) => (
                <div className="list-item" key={x.id}>
                  <div className="grow">
                    <b>{x.className}</b>
                    <div className="small muted">{fmtDateTime(x.startAt)} · {x.bookedCount} đăng ký</div>
                  </div>
                  <Badge color="amber">Chưa điểm danh</Badge>
                </div>
              ))}
            </div>
          )}
          <div className="divider" />
          <h3 className="mb16">Đánh giá gần đây</h3>
          {apiReviews && apiReviews.reviews.length > 0 ? (
            apiReviews.reviews.slice(0, 3).map((r) => (
              <div key={r.id} className="mb16">
                <div className="row">
                  <Stars value={r.rating} size={13} />
                  <span className="small dim">{r.memberName} · {fmtDate(r.createdAt)}</span>
                </div>
                <div className="small muted">{r.comment}</div>
              </div>
            ))
          ) : (
            <div className="small muted">Chưa có đánh giá nào.</div>
          )}
        </div>
      </div>
    </>
  );
}

export default TrainerHomeScreen;
