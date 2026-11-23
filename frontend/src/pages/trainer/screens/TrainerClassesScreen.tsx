import { useCallback, useEffect, useMemo, useState } from 'react';
import { CalendarDays, MapPin, ClipboardCheck, RefreshCw } from 'lucide-react';
import { useApp } from '../../../store/AppContext';
import * as L from '../../../store/logic';
import { Avatar, Badge, Empty, Modal, PageHead, Tabs } from '../../../components/ui';
import { cbLabel } from '../../../components/labels';
import { fmtDateTime, fmtTime, fmtWeekday } from '../../../utils';
import { useTrainerStats } from './useTrainerStats';
import { classesApi, type ApiClassSessionDetailDto, type ApiClassSessionDto } from '../../../api/classesApi';
import { getAuthToken, isApiNetworkError } from '../../../api/apiClient';

function bookingStatusBadge(status: string): [string, string] {
  switch (status) {
    case 'Confirmed':
      return ['Đã đặt', 'green'];
    case 'Attended':
      return ['Đã tham gia', 'blue'];
    case 'NoShow':
      return ['Vắng mặt', 'red'];
    case 'Cancelled':
      return ['Đã hủy', 'gray'];
    case 'LateCancelled':
      return ['Hủy muộn', 'red'];
    case 'AutoCancelled':
      return ['Hủy tự động', 'purple'];
    default:
      return [status, 'gray'];
  }
}

export function TrainerClassesScreen() {
  const { db, user, toast } = useApp();
  const [tab, setTab] = useState<'up' | 'mark' | 'done'>('mark');
  const [sel, setSel] = useState<string | null>(null);
  const [apiMode, setApiMode] = useState<boolean>(!!getAuthToken());
  const [loading, setLoading] = useState<boolean>(false);
  const [apiSessions, setApiSessions] = useState<ApiClassSessionDto[]>([]);

  const s = useTrainerStats();

  const loadTrainerSessions = useCallback(async () => {
    if (!getAuthToken()) {
      setApiMode(false);
      return;
    }
    setLoading(true);
    try {
      const data = await classesApi.getTrainerMySessions('all');
      setApiSessions(data);
      setApiMode(true);
    } catch (err) {
      if (isApiNetworkError(err)) {
        setApiMode(false);
      } else {
        toast((err as Error).message, 'error');
      }
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    loadTrainerSessions();
  }, [loadTrainerSessions]);

  const { toMark, upcoming, done, classNamesSummary } = useMemo(() => {
    if (apiMode) {
      const now = Date.now();
      const mark = apiSessions
        .filter((x) => !['CANCELLED', 'COMPLETED'].includes(x.status) && new Date(x.startAt).getTime() <= now)
        .sort((a, b) => b.startAt.localeCompare(a.startAt));
      const up = apiSessions
        .filter((x) => !['CANCELLED', 'COMPLETED'].includes(x.status) && new Date(x.startAt).getTime() > now)
        .sort((a, b) => a.startAt.localeCompare(b.startAt));
      const completed = apiSessions
        .filter((x) => x.status === 'COMPLETED')
        .sort((a, b) => b.startAt.localeCompare(a.startAt));
      const names = Array.from(new Set(apiSessions.map((x) => x.className)));
      return {
        toMark: mark,
        upcoming: up,
        done: completed,
        classNamesSummary: names.length > 0 ? `Phụ trách ${names.length} lớp: ${names.join(', ')}` : 'Chưa có buổi học được phân công',
      };
    }

    const myClasses = db.classes.filter((c) => c.trainerId === user!.id && c.active);
    const mapLocal = (x: (typeof s.sessions)[number]): ApiClassSessionDto => {
      const c = L.classById(db, x.classId)!;
      const bk = L.sessionBookings(db, x.id);
      const booked = bk.filter((b) => ['booked', 'attended', 'no_show'].includes(b.status)).length;
      const att = bk.filter((b) => b.status === 'attended').length;
      return {
        id: x.id,
        classId: x.classId,
        className: c.name,
        category: c.category,
        color: c.color,
        branchId: c.branchId,
        branchName: L.branchById(db, c.branchId)?.name ?? '',
        trainerId: x.trainerId,
        trainerUserId: x.trainerId,
        trainerName: user!.fullName,
        room: x.room,
        capacity: c.capacity,
        sessionDate: x.date,
        startAt: x.startAt,
        endAt: x.endAt,
        durationMinutes: c.durationMin,
        status: x.status === 'cancelled' ? 'CANCELLED' : x.attendanceConfirmed ? 'COMPLETED' : 'SCHEDULED',
        bookedCount: booked,
        availableSlots: Math.max(0, c.capacity - booked),
        waitlistCount: L.waitlistOf(db, x.id).length,
        attendanceConfirmed: x.attendanceConfirmed,
        attendedCount: att,
        canJoinWaitlist: false,
        hoursUntilStart: 0,
        averageRating: 5,
        reviewCount: 0,
      };
    };

    return {
      toMark: [...s.toMark].reverse().map(mapLocal),
      upcoming: s.upcomingSessions.map(mapLocal),
      done: s.sessions.filter((x) => x.attendanceConfirmed).reverse().map(mapLocal),
      classNamesSummary: `Phụ trách ${myClasses.length} lớp: ${myClasses.map((c) => c.name).join(', ')}`,
    };
  }, [apiMode, apiSessions, db, s.sessions, s.toMark, s.upcomingSessions, user]);

  const list = tab === 'up' ? upcoming : tab === 'mark' ? toMark : done;

  return (
    <>
      <PageHead
        title="Lớp dạy & điểm danh"
        desc={classNamesSummary}
        actions={
          apiMode ? (
            <button className="btn sm" onClick={loadTrainerSessions} disabled={loading}>
              <RefreshCw size={14} /> {loading ? 'Đang tải...' : 'Làm mới'}
            </button>
          ) : undefined
        }
      />

      <Tabs
        value={tab}
        onChange={setTab}
        tabs={[
          { id: 'mark', label: 'Cần điểm danh', count: toMark.length },
          { id: 'up', label: 'Sắp tới', count: upcoming.length },
          { id: 'done', label: 'Đã điểm danh', count: done.length },
        ]}
      />

      {list.length === 0 ? (
        <div className="card">
          <Empty text="Không có buổi học" />
        </div>
      ) : (
        <div className="grid g-auto">
          {list.slice(0, 30).map((x) => (
            <div
              className="card hover"
              key={x.id}
              style={{ borderTop: `3px solid ${x.color}`, cursor: 'pointer' }}
              onClick={() => setSel(x.id)}
              id={`trainer-session-${x.id}`}
            >
              <div className="row between">
                <b>{x.className}</b>
                {x.status === 'CANCELLED' ? (
                  <Badge color="red">Đã hủy</Badge>
                ) : x.attendanceConfirmed ? (
                  <Badge color="green">
                    {x.attendedCount}/{x.bookedCount} có mặt
                  </Badge>
                ) : (
                  <Badge color="gray">
                    {x.bookedCount}/{x.capacity}
                  </Badge>
                )}
              </div>
              <div className="small muted mt8">
                <CalendarDays size={13} /> {fmtWeekday(x.startAt)}, {fmtDateTime(x.startAt)}
              </div>
              <div className="small muted">
                <MapPin size={13} /> {x.room} · {x.branchName}
              </div>
              {x.waitlistCount > 0 && (
                <div className="small mt8" style={{ color: '#ffc857' }}>
                  {x.waitlistCount} người trong danh sách chờ
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      {sel && (
        <AttendanceModal
          sessionId={sel}
          apiMode={apiMode}
          onSaved={loadTrainerSessions}
          onClose={() => setSel(null)}
        />
      )}
    </>
  );
}

function AttendanceModal({
  sessionId,
  apiMode,
  onSaved,
  onClose,
}: {
  sessionId: string;
  apiMode: boolean;
  onSaved: () => Promise<void>;
  onClose: () => void;
}) {
  const { db, act, toast } = useApp();
  const [detail, setDetail] = useState<ApiClassSessionDetailDto | null>(null);
  const [loading, setLoading] = useState<boolean>(apiMode);
  const [saving, setSaving] = useState<boolean>(false);
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  useEffect(() => {
    if (!apiMode) {
      const s = L.sessionById(db, sessionId);
      if (s) {
        const c = L.classById(db, s.classId)!;
        const bookings = L.sessionBookings(db, s.id).filter((b) => ['booked', 'attended', 'no_show'].includes(b.status));
        const checked = (mid: string, b: { checkedIn: boolean }) => b.checkedIn || L.hasCheckinOn(db, mid, c.branchId, s.date);
        setSelectedIds(
          bookings.filter((b) => b.status === 'attended' || (b.status === 'booked' && checked(b.memberId, b))).map((b) => b.memberId),
        );
      }
      return;
    }

    let cancelled = false;
    setLoading(true);
    classesApi
      .getSessionDetail(sessionId)
      .then((d) => {
        if (cancelled) return;
        setDetail(d);
        setSelectedIds(
          d.roster
            .filter((r) => r.bookingStatus === 'Attended' || (r.bookingStatus === 'Confirmed' && r.hasCheckedInAtBranch))
            .map((r) => r.bookingId),
        );
      })
      .catch((err) => {
        if (!cancelled) toast((err as Error).message, 'error');
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [apiMode, db, sessionId, toast]);

  if (apiMode) {
    const started = detail ? new Date(detail.startAt).getTime() <= Date.now() : false;
    const canSubmit = !!detail && started && detail.status !== 'CANCELLED';

    const saveApi = async () => {
      if (!detail) return;
      setSaving(true);
      try {
        const res = await classesApi.confirmAttendance(detail.id, { attendedBookingIds: selectedIds });
        toast(res.message, res.rejectedNotCheckedInCount > 0 ? 'warning' : 'success');
        await onSaved();
        onClose();
      } catch (err) {
        toast((err as Error).message, 'error');
      } finally {
        setSaving(false);
      }
    };

    return (
      <Modal
        wide
        title={detail ? `${detail.className} · ${fmtDateTime(detail.startAt)}` : 'Điểm danh buổi học'}
        onClose={onClose}
        footer={
          canSubmit ? (
            <>
              <button className="btn" onClick={onClose}>
                Đóng
              </button>
              <button className="btn primary" disabled={saving} onClick={saveApi} id="attendance-save">
                <ClipboardCheck size={16} /> {saving ? 'Đang lưu...' : 'Xác nhận danh sách tham gia'}
              </button>
            </>
          ) : undefined
        }
      >
        {loading ? (
          <div className="muted">Đang tải danh sách học viên & dữ liệu check-in...</div>
        ) : !detail ? (
          <Empty text="Không tải được chi tiết buổi học" />
        ) : (
          <>
            {!started && <div className="card mb16 small muted">Buổi học chưa bắt đầu – điểm danh sẽ mở khi đến giờ học.</div>}
            {detail.roster.length === 0 ? (
              <Empty text="Chưa có học viên đăng ký" />
            ) : (
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Có mặt</th>
                      <th>Hội viên</th>
                      <th>Check-in tại CN</th>
                      <th>Trạng thái</th>
                    </tr>
                  </thead>
                  <tbody>
                    {detail.roster.map((r) => {
                      const badge = bookingStatusBadge(r.bookingStatus);
                      return (
                        <tr key={r.bookingId}>
                          <td>
                            <label className="check">
                              <input
                                type="checkbox"
                                disabled={!canSubmit}
                                checked={selectedIds.includes(r.bookingId)}
                                onChange={(e) =>
                                  setSelectedIds(
                                    e.target.checked
                                      ? [...selectedIds, r.bookingId]
                                      : selectedIds.filter((x) => x !== r.bookingId),
                                  )
                                }
                              />
                            </label>
                          </td>
                          <td>
                            <div className="row">
                              <Avatar user={{ fullName: r.memberName, avatarColor: detail.color }} size="sm" />
                              <div>
                                <b>{r.memberName}</b>
                                <div className="small dim">{r.cardCode || r.memberEmail}</div>
                              </div>
                            </div>
                          </td>
                          <td>
                            {r.hasCheckedInAtBranch ? (
                              <Badge color="green">
                                Đã check-in{r.checkInAt ? ` (${fmtTime(r.checkInAt)})` : ''}
                              </Badge>
                            ) : (
                              <Badge color="gray">Chưa check-in</Badge>
                            )}
                          </td>
                          <td>
                            <Badge color={badge[1]}>{badge[0]}</Badge>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
            <div className="small dim mt8">
              * Chỉ hội viên đã check-in hợp lệ tại chi nhánh trong ngày mới được ghi nhận tham gia và mở quyền đánh giá.
            </div>
            {detail.waitlist.length > 0 && (
              <>
                <h3 className="mt16 mb16">Danh sách chờ ({detail.waitlist.length})</h3>
                <div className="col">
                  {detail.waitlist.map((w) => (
                    <div className="row small" key={w.id}>
                      <Badge color="amber" plain>
                        #{w.position}
                      </Badge>
                      <b>{w.memberName}</b>
                      <span className="dim">
                        · {w.planName || 'Gói tập'} · ưu tiên {w.priorityLevel} · {fmtDateTime(w.registeredAt)}
                      </span>
                    </div>
                  ))}
                </div>
              </>
            )}
          </>
        )}
      </Modal>
    );
  }

  // Fallback offline
  const s = L.sessionById(db, sessionId)!;
  const c = L.classById(db, s.classId)!;
  const bookings = L.sessionBookings(db, s.id).filter((b) => ['booked', 'attended', 'no_show'].includes(b.status));
  const waitlist = L.waitlistOf(db, s.id);
  const checked = (mid: string, b: { checkedIn: boolean }) => b.checkedIn || L.hasCheckinOn(db, mid, c.branchId, s.date);
  const started = new Date(s.startAt).getTime() <= Date.now();
  const saveOffline = () => {
    act(
      (d) => L.confirmAttendance(d, s.id, selectedIds),
      (rej) => (rej ? `Đã lưu điểm danh. ${rej} hội viên chưa check-in hợp lệ bị ghi vắng.` : 'Đã xác nhận danh sách tham gia'),
    );
    onClose();
  };

  return (
    <Modal
      wide
      title={`${c.name} · ${fmtDateTime(s.startAt)}`}
      onClose={onClose}
      footer={
        started && s.status !== 'cancelled' ? (
          <>
            <button className="btn" onClick={onClose}>
              Đóng
            </button>
            <button className="btn primary" onClick={saveOffline} id="attendance-save">
              <ClipboardCheck size={16} /> Xác nhận danh sách tham gia
            </button>
          </>
        ) : undefined
      }
    >
      {!started && <div className="card mb16 small muted">Buổi học chưa bắt đầu – điểm danh sẽ mở khi đến giờ học.</div>}
      {bookings.length === 0 ? (
        <Empty text="Chưa có học viên đăng ký" />
      ) : (
        <div className="table-wrap">
          <table>
            <thead>
              <tr>
                <th>Có mặt</th>
                <th>Hội viên</th>
                <th>Check-in tại CN</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {bookings.map((b) => {
                const m = L.userById(db, b.memberId);
                const ok = checked(b.memberId, b);
                return (
                  <tr key={b.id}>
                    <td>
                      <label className="check">
                        <input
                          type="checkbox"
                          disabled={!started || s.status === 'cancelled'}
                          checked={selectedIds.includes(b.memberId)}
                          onChange={(e) =>
                            setSelectedIds(
                              e.target.checked ? [...selectedIds, b.memberId] : selectedIds.filter((x) => x !== b.memberId),
                            )
                          }
                        />
                      </label>
                    </td>
                    <td>
                      <div className="row">
                        <Avatar user={m} size="sm" />
                        <div>
                          <b>{m?.fullName}</b>
                          <div className="small dim">{m?.cardCode}</div>
                        </div>
                      </div>
                    </td>
                    <td>{ok ? <Badge color="green">Đã check-in</Badge> : <Badge color="gray">Chưa check-in</Badge>}</td>
                    <td>
                      <Badge color={cbLabel[b.status][1]}>{cbLabel[b.status][0]}</Badge>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
      <div className="small dim mt8">* Chỉ hội viên đã check-in hợp lệ tại chi nhánh trong ngày mới được ghi nhận tham gia và mở quyền đánh giá.</div>
      {waitlist.length > 0 && (
        <>
          <h3 className="mt16 mb16">Danh sách chờ ({waitlist.length})</h3>
          <div className="col">
            {waitlist.map((b, i) => (
              <div className="row small" key={b.id}>
                <Badge color="amber" plain>
                  #{i + 1}
                </Badge>
                {L.userById(db, b.memberId)?.fullName}
                <span className="dim">
                  · ưu tiên {b.priority} · {fmtDateTime(b.createdAt)}
                </span>
              </div>
            ))}
          </div>
        </>
      )}
    </Modal>
  );
}

export default TrainerClassesScreen;
