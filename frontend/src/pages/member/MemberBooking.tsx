import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  CalendarDays,
  Users,
  Clock,
  MapPin,
  Dumbbell,
  Star,
  Award,
  Gift,
  MessageSquare,
  X,
  ListOrdered,
  Search,
  Sunrise,
  Sun,
  Sunset,
  CheckCircle2,
  Sparkles,
  LayoutGrid,
  List,
  Settings,
  ArrowLeft,
  ArrowRight,
  RefreshCw,
} from 'lucide-react';
import { useApp } from '../../store/AppContext';
import * as L from '../../store/logic';
import { Avatar, Badge, Empty, Modal, PageHead, Stars, Tabs, ConfirmModal, Progress } from '../../components/ui';
import { cbLabel, ptbLabel } from '../../components/labels';
import { addDays, todayStr, fmtTime, fmtDate, WEEKDAYS, parseDate, fmtDateTime, hoursUntil, fmtWeekday, toDateStr, atTime } from '../../utils';
import { classesApi, type ApiClassSessionDetailDto, type ApiClassSessionDto } from '../../api/classesApi';
import { bookingsApi, type ApiMyScheduleSummaryDto } from '../../api/bookingsApi';
import { branchesApi, type BranchApiDto } from '../../api/branchesApi';
import { ptApi, type ApiPtBalanceDto } from '../../api/ptApi';
import { emitAppDataChanged } from '../../api/notificationsApi';
import { trainerService, type TrainerAvailabilityItem, type TrainerItem } from '../../services/trainer.service';
import { getAuthToken, isApiNetworkError } from '../../api/apiClient';

type PeriodKey = 'morning' | 'afternoon' | 'evening';

function getPeriodKey(isoStart: string): PeriodKey {
  const h = new Date(isoStart).getHours();
  if (h < 12) return 'morning';
  if (h < 17) return 'afternoon';
  return 'evening';
}

/** 10 Ca tập tiêu chuẩn trong ngày (hiển thị cột trái Ca 1..10 & cột phải Giờ bắt đầu như mẫu TKB) */
export interface GymSlotDef {
  id: number;
  label: string;
  timeLabel: string;
  minMinutes: number;
  maxMinutes: number;
  isEvening?: boolean;
}

export const GYM_TIME_SLOTS: GymSlotDef[] = [
  { id: 1, label: 'Ca 1', timeLabel: '06:30', minMinutes: 0, maxMinutes: 7 * 60 + 14 },
  { id: 2, label: 'Ca 2', timeLabel: '07:30', minMinutes: 7 * 60 + 15, maxMinutes: 8 * 60 + 29 },
  { id: 3, label: 'Ca 3', timeLabel: '09:00', minMinutes: 8 * 60 + 30, maxMinutes: 9 * 60 + 59 },
  { id: 4, label: 'Ca 4', timeLabel: '10:15', minMinutes: 10 * 60, maxMinutes: 12 * 60 + 59 },
  { id: 5, label: 'Ca 5', timeLabel: '14:00', minMinutes: 13 * 60, maxMinutes: 15 * 60 + 14 },
  { id: 6, label: 'Ca 6', timeLabel: '15:30', minMinutes: 15 * 60 + 15, maxMinutes: 16 * 60 + 59 },
  { id: 7, label: 'Ca 7', timeLabel: '17:30', minMinutes: 17 * 60, maxMinutes: 18 * 60 + 14, isEvening: true },
  { id: 8, label: 'Ca 8', timeLabel: '18:30', minMinutes: 18 * 60 + 15, maxMinutes: 19 * 60 + 14, isEvening: true },
  { id: 9, label: 'Ca 9', timeLabel: '19:30', minMinutes: 19 * 60 + 15, maxMinutes: 20 * 60 + 29, isEvening: true },
  { id: 10, label: 'Ca 10', timeLabel: '20:30', minMinutes: 20 * 60 + 30, maxMinutes: 23 * 60 + 59, isEvening: true },
];

export function matchSlotId(isoStart: string): number {
  const dt = new Date(isoStart);
  const mins = dt.getHours() * 60 + dt.getMinutes();
  const found = GYM_TIME_SLOTS.find((s) => mins >= s.minMinutes && mins <= s.maxMinutes);
  return found ? found.id : 1;
}

/** Tính ngày Thứ 2 đầu tuần của một ngày YYYY-MM-DD */
export function getMondayStr(baseDate: string, weekDiff = 0): string {
  const d = parseDate(baseDate);
  const day = d.getDay(); // 0 = CN, 1 = T2 ... 6 = T7
  const diffToMon = day === 0 ? -6 : 1 - day;
  return addDays(baseDate, diffToMon + weekDiff * 7);
}

export function fullDayName(dateStr: string): string {
  const day = parseDate(dateStr).getDay();
  return day === 0 ? 'Chủ Nhật' : `Thứ ${day + 1}`;
}

export function shortDayMonth(dateStr: string): string {
  const d = parseDate(dateStr);
  return `${String(d.getDate()).padStart(2, '0')}/${String(d.getMonth() + 1).padStart(2, '0')}`;
}

type SessionCardVM = ApiClassSessionDto & { localDate: string };

/* ================= Lớp học nhóm theo danh sách tuần ================= */
function formatMemberWeekLabel(offset: number): string {
  const mon = getMondayStr(todayStr(), offset);
  const sun = addDays(mon, 6);
  const range = `${shortDayMonth(mon)} – ${fmtDate(sun)}`;
  if (offset === 0) return `Tuần này (${range})`;
  if (offset === -1) return `Tuần trước (${range})`;
  if (offset === 1) return `Tuần sau (${range})`;
  return `Tuần +${offset} (${range})`;
}

export function MemberClasses() {
  const { db, user, act, toast } = useApp();
  const [weekIdx, setWeekIdx] = useState<number>(0);
  const [branch, setBranch] = useState<string>('all');
  const [cat, setCat] = useState<string>('all');
  const [scheduleScope, setScheduleScope] = useState<'all_classes' | 'my_schedule'>('all_classes');
  const [onlyAvailable, setOnlyAvailable] = useState<boolean>(false);
  const [selId, setSelId] = useState<string | null>(null);
  const [nowMs, setNowMs] = useState<number>(() => Date.now());

  const [apiMode, setApiMode] = useState<boolean>(true);
  const [loading, setLoading] = useState<boolean>(false);
  const [apiBranches, setApiBranches] = useState<{ id: string; name: string; address: string }[]>([]);
  const [apiSessions, setApiSessions] = useState<ApiClassSessionDto[]>([]);
  const [apiSchedule, setApiSchedule] = useState<ApiMyScheduleSummaryDto | null>(null);

  const monday = useMemo(() => getMondayStr(todayStr(), weekIdx), [weekIdx]);
  const days = useMemo(() => Array.from({ length: 7 }, (_, i) => addDays(monday, i)), [monday]);

  const weekOffsets = useMemo(() => {
    const base = [-1, 0, 1, 2, 3, 4];
    if (!base.includes(weekIdx)) {
      base.push(weekIdx);
      base.sort((a, b) => a - b);
    }
    return base;
  }, [weekIdx]);

  const loadFromApi = useCallback(async () => {
    setLoading(true);
    try {
      const from = atTime(days[0], '00:00');
      const to = atTime(days[6], '23:59');
      const [brList, sesList] = await Promise.all([
        branchesApi.getAll(),
        classesApi.getSessions({ from, to }),
      ]);
      setApiBranches(
        brList.map((b) => ({
          id: b.id,
          name: b.name,
          address: b.address || '',
        })),
      );
      setApiSessions(sesList);
      setNowMs(Date.now());
      setApiMode(true);

      if (getAuthToken()) {
        try {
          const sched = await bookingsApi.getMySchedule();
          setApiSchedule(sched);
        } catch {
          setApiSchedule(null);
        }
      } else {
        setApiSchedule(null);
      }
    } catch (err) {
      if (isApiNetworkError(err)) {
        setApiMode(false);
      } else {
        toast((err as Error).message, 'error');
      }
    } finally {
      setLoading(false);
    }
  }, [days, toast]);

  useEffect(() => {
    let cancelled = false;
    const from = atTime(days[0], '00:00');
    const to = atTime(days[6], '23:59');

    Promise.all([
      branchesApi.getAll(),
      classesApi.getSessions({ from, to }),
      getAuthToken() ? bookingsApi.getMySchedule().catch(() => null) : Promise.resolve(null),
    ])
      .then(([brList, sesList, sched]) => {
        if (cancelled) return;
        setApiBranches(
          brList.map((b) => ({
            id: b.id,
            name: b.name,
            address: b.address || '',
          })),
        );
        setApiSessions(sesList);
        setApiSchedule(sched);
        setNowMs(Date.now());
        setApiMode(true);
      })
      .catch((err) => {
        if (cancelled) return;
        if (isApiNetworkError(err)) {
          setApiMode(false);
        } else {
          toast((err as Error).message, 'error');
        }
      });

    return () => {
      cancelled = true;
    };
  }, [days, toast]);

  const branchOptions = useMemo(
    () =>
      apiMode
        ? apiBranches
        : db.branches.map((b) => ({ id: b.id, name: b.name, address: b.address })),
    [apiMode, apiBranches, db.branches],
  );

  const allWeekSessions: SessionCardVM[] = useMemo(() => {
    const rawList: SessionCardVM[] = apiMode
      ? apiSessions.map((s) => ({
          ...s,
          localDate: toDateStr(new Date(s.startAt)),
        }))
      : db.sessions
          .filter((s) => days.includes(s.date))
          .map((s) => {
            const c = L.classById(db, s.classId)!;
            const tr = L.userById(db, s.trainerId);
            const br = L.branchById(db, c.branchId);
            const cnt = L.bookedCount(db, s.id);
            const wl = L.waitlistOf(db, s.id);
            const mineBooking = db.classBookings.find(
              (b) =>
                b.sessionId === s.id &&
                b.memberId === user!.id &&
                ['booked', 'waitlist', 'attended', 'no_show'].includes(b.status),
            );
            const revs = db.reviews.filter((r) => r.classId === c.id);
            const avg = revs.length ? revs.reduce((acc, r) => acc + r.rating, 0) / revs.length : 5.0;
            const hrs = (new Date(s.startAt).getTime() - nowMs) / 3600000;
            const wlPos = mineBooking?.status === 'waitlist' ? wl.findIndex((x) => x.id === mineBooking.id) + 1 : null;

            return {
              id: s.id,
              classId: s.classId,
              className: c.name,
              category: c.category,
              color: c.color,
              branchId: c.branchId,
              branchName: br?.name ?? '',
              trainerId: s.trainerId,
              trainerUserId: s.trainerId,
              trainerName: tr?.fullName ?? '',
              room: s.room,
              capacity: c.capacity,
              sessionDate: s.date,
              startAt: s.startAt,
              endAt: s.endAt,
              durationMinutes: c.durationMin,
              status:
                s.status === 'cancelled'
                  ? 'CANCELLED'
                  : s.status === 'completed'
                    ? 'COMPLETED'
                    : cnt >= c.capacity
                      ? 'FULL'
                      : 'SCHEDULED',
              bookedCount: cnt,
              availableSlots: Math.max(0, c.capacity - cnt),
              waitlistCount: wl.length,
              attendanceConfirmed: s.attendanceConfirmed,
              attendedCount: 0,
              canJoinWaitlist:
                s.status === 'scheduled' && cnt >= c.capacity && hrs > db.settings.waitlistCutoffHours,
              hoursUntilStart: hrs,
              averageRating: avg,
              reviewCount: revs.length,
              myBookingId: mineBooking?.status !== 'waitlist' ? mineBooking?.id ?? null : null,
              myBookingStatus:
                mineBooking?.status === 'booked'
                  ? 'Confirmed'
                  : mineBooking?.status === 'attended'
                    ? 'Attended'
                    : null,
              myWaitlistEntryId: mineBooking?.status === 'waitlist' ? mineBooking.id : null,
              myWaitlistPosition: wlPos,
              localDate: s.date,
            };
          });

    return Array.from(new Map(rawList.map((item) => [item.id, item])).values());
  }, [apiMode, apiSessions, db, days, user, nowMs]);

  const isMySession = (s: SessionCardVM) => !!s.myBookingId || !!s.myWaitlistEntryId;

  const baseFilteredSessions = useMemo(
    () =>
      allWeekSessions
        .filter((s) => days.includes(s.localDate))
        .filter((s) => {
          if (branch !== 'all' && s.branchId !== branch) return false;
          if (scheduleScope === 'my_schedule' && !isMySession(s)) return false;
          if (onlyAvailable && (s.bookedCount >= s.capacity || s.status === 'CANCELLED')) return false;
          return true;
        })
        .sort((a, b) => a.startAt.localeCompare(b.startAt)),
    [allWeekSessions, days, branch, scheduleScope, onlyAvailable],
  );

  const categoryCounts = useMemo(() => {
    const map = new Map<string, { category: string; color: string; count: number }>();
    for (const s of baseFilteredSessions) {
      const name = s.category || 'Khác';
      const existing = map.get(name);
      if (existing) {
        existing.count += 1;
      } else {
        map.set(name, { category: name, color: s.color || '#7c5cff', count: 1 });
      }
    }
    return Array.from(map.values()).sort((a, b) => a.category.localeCompare(b.category, 'vi'));
  }, [baseFilteredSessions]);

  const effectiveCat = useMemo(
    () => (cat !== 'all' && categoryCounts.some((c) => c.category === cat) ? cat : 'all'),
    [cat, categoryCounts],
  );

  const sessions = useMemo(
    () =>
      effectiveCat === 'all'
        ? baseFilteredSessions
        : baseFilteredSessions.filter((s) => (s.category || 'Khác') === effectiveCat),
    [baseFilteredSessions, effectiveCat],
  );

  const activeCategoryColor =
    effectiveCat === 'all'
      ? 'var(--primary)'
      : categoryCounts.find((c) => c.category === effectiveCat)?.color ?? 'var(--primary)';

  const waitlistCutoff = apiMode
    ? (apiSchedule?.waitlistCutoffHours ?? db.settings.waitlistCutoffHours)
    : db.settings.waitlistCutoffHours;

  const localMembership = L.membershipOn(db, user!.id);
  const localPlan = localMembership ? L.planById(db, localMembership.planId) : undefined;

  const selectedSession = selId ? allWeekSessions.find((x) => x.id === selId) ?? null : null;

  return (
    <>
      <PageHead
        title="Đăng ký Lớp học Nhóm"
        desc={
          apiMode ? (
            apiSchedule?.activePlanName ? (
              <>
                Gói <b>{apiSchedule.activePlanName}</b> ({apiSchedule.membershipStatus}) · Hạn mức{' '}
                <b>
                  {apiSchedule.maxBookingsPerMonth > 0
                    ? `${apiSchedule.usedBookingsThisMonth}/${apiSchedule.maxBookingsPerMonth} lượt/tháng`
                    : 'Không giới hạn'}
                </b>{' '}
                · Đóng Waitlist trước <b>{apiSchedule.waitlistCutoffHours}h</b>
              </>
            ) : getAuthToken() ? (
              'Bạn chưa có gói hội viên còn hiệu lực hoặc đang tạm ngưng.'
            ) : (
              'Đang đồng bộ lịch trực tiếp từ máy chủ OOAD.'
            )
          ) : localPlan ? (
            <>
              Gói <b>{localPlan.name}</b> · Hạn mức{' '}
              <b>{localPlan.classBookingsPerWeek ? `${localPlan.classBookingsPerWeek} lớp/tuần` : 'Không giới hạn'}</b>{' '}
              · {L.scopeLabel(db, localPlan.branchScope)} · Đóng Waitlist trước{' '}
              <b>{db.settings.waitlistCutoffHours}h</b>
            </>
          ) : (
            'Bạn cần gói hội viên còn hiệu lực để đặt lịch lớp học nhóm.'
          )
        }
        actions={
          <button className="btn sm" onClick={loadFromApi} disabled={loading} title="Tải lại lịch mới nhất">
            <RefreshCw size={14} /> {loading ? 'Đang tải...' : 'Làm mới'}
          </button>
        }
      />

      <div className="card mb12" style={{ padding: '10px 14px' }}>
        <div className="row between wrap mb8" style={{ gap: 8 }}>
          <div className="row wrap" style={{ gap: 5 }}>
            <CalendarDays size={15} color="var(--primary)" style={{ flexShrink: 0 }} />
            <button
              type="button"
              className="btn sm"
              style={{ fontSize: 12, padding: '5px 8px' }}
              onClick={() => {
                setWeekIdx((w) => w - 1);
                setNowMs(Date.now());
              }}
              title="Tuần trước"
              id="member-prev-week"
            >
              <ArrowLeft size={13} /> Trước
            </button>

            <select
              className="input sm"
              style={{ width: 'auto', minWidth: 195, fontSize: 12.5, padding: '5px 9px' }}
              value={weekIdx}
              onChange={(e) => {
                setWeekIdx(Number(e.target.value));
                setNowMs(Date.now());
              }}
              id="member-week-select"
            >
              {weekOffsets.map((offset) => (
                <option key={offset} value={offset}>
                  {formatMemberWeekLabel(offset)}
                </option>
              ))}
            </select>

            <button
              type="button"
              className="btn sm"
              style={{ fontSize: 12, padding: '5px 9px' }}
              disabled={weekIdx === 0}
              onClick={() => {
                setWeekIdx(0);
                setNowMs(Date.now());
              }}
              id="member-this-week"
            >
              Tuần này
            </button>

            <button
              type="button"
              className="btn sm"
              style={{ fontSize: 12, padding: '5px 8px' }}
              onClick={() => {
                setWeekIdx((w) => w + 1);
                setNowMs(Date.now());
              }}
              title="Tuần sau"
              id="member-next-week"
            >
              Sau <ArrowRight size={13} />
            </button>

            {loading && (
              <span className="small muted" style={{ fontSize: 11.5, marginLeft: 4 }}>
                Đang tải...
              </span>
            )}
          </div>

          <div className="row wrap" style={{ gap: 8 }}>
            <select
              className="input sm"
              style={{ width: 'auto', maxWidth: 210, fontSize: 12.5, padding: '5px 9px' }}
              value={branch}
              onChange={(e) => setBranch(e.target.value)}
              id="member-branch-filter"
            >
              <option value="all">Tất cả chi nhánh ({branchOptions.length})</option>
              {branchOptions.map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>

            <select
              className="input sm"
              style={{ width: 'auto', fontSize: 12.5, padding: '5px 9px' }}
              value={scheduleScope}
              onChange={(e) => setScheduleScope(e.target.value as 'all_classes' | 'my_schedule')}
              id="member-scope-filter"
            >
              <option value="all_classes">Tất cả lớp</option>
              <option value="my_schedule">Lớp tôi đã đặt</option>
            </select>

            <label className="check small bold" style={{ fontSize: 12.5 }}>
              <input
                type="checkbox"
                checked={onlyAvailable}
                onChange={(e) => setOnlyAvailable(e.target.checked)}
              />
              <span>Chỉ lớp còn chỗ</span>
            </label>
          </div>
        </div>

        <div className="row wrap" style={{ gap: 6, paddingTop: 8, borderTop: '1px solid var(--border)' }}>
          <span className="small muted bold" style={{ fontSize: 12 }}>Bộ môn:</span>
          <div className="chips">
            <button
              type="button"
              className={`chip ${effectiveCat === 'all' ? 'on' : ''}`}
              style={{ fontSize: 11.5, padding: '4px 10px' }}
              onClick={() => setCat('all')}
              id="member-cat-all"
            >
              Tất cả ({baseFilteredSessions.length})
            </button>
            {categoryCounts.map((c) => (
              <button
                type="button"
                key={c.category}
                className={`chip ${effectiveCat === c.category ? 'on' : ''}`}
                style={{ fontSize: 11.5, padding: '4px 10px' }}
                onClick={() => setCat(c.category)}
                id={`member-cat-${c.category}`}
              >
                <span
                  style={{
                    width: 7,
                    height: 7,
                    borderRadius: 99,
                    background: c.color,
                    display: 'inline-block',
                    marginRight: 4,
                  }}
                />
                {c.category} ({c.count})
              </button>
            ))}
          </div>
        </div>
      </div>

      <div
        className="card"
        style={{
          padding: '12px 14px',
          borderLeft: effectiveCat === 'all' ? undefined : `4px solid ${activeCategoryColor}`,
        }}
      >
        <div className="row between mb12" style={{ gap: 8 }}>
          <div className="row" style={{ gap: 8 }}>
            <span
              style={{
                width: 9,
                height: 9,
                borderRadius: 3,
                background: activeCategoryColor,
                display: 'inline-block',
              }}
            />
            <h2 style={{ margin: 0, fontSize: 14 }}>
              {effectiveCat === 'all'
                ? 'Danh sách lớp học nhóm — Tất cả bộ môn'
                : `Bộ môn: ${effectiveCat}`}
            </h2>
          </div>
          <span className="small muted" style={{ fontSize: 12 }}>
            <b>{sessions.length}</b> buổi trong tuần
          </span>
        </div>

        {sessions.length === 0 ? (
          <div style={{ padding: 24 }}>
            <Empty
              text="Không có lớp học nào trong tuần này phù hợp bộ lọc."
              icon={<CalendarDays size={36} />}
            />
          </div>
        ) : (
          <div className="table-wrap">
            <table className="admin-compact-table">
              <thead>
                <tr>
                  <th>Lớp học</th>
                  <th>Thời gian</th>
                  <th>Địa điểm &amp; HLV</th>
                  <th>Sĩ số</th>
                  <th>Trạng thái</th>
                  <th style={{ textAlign: 'right' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody key={`${effectiveCat}-${branch}-${weekIdx}-${scheduleScope}-${onlyAvailable}`}>
                {sessions.map((s) => {
                  const full = s.bookedCount >= s.capacity;
                  const isCancelled = s.status === 'CANCELLED';
                  const isCompleted = s.status === 'COMPLETED';
                  const past = new Date(s.startAt).getTime() <= nowMs;
                  const hrs = (new Date(s.startAt).getTime() - nowMs) / 3600000;
                  const hasConfirmed = !!s.myBookingId && s.myBookingStatus === 'Confirmed';
                  const hasWaitlist = !!s.myWaitlistEntryId;
                  const startDateObj = new Date(s.startAt);
                  const dateLabel = `${WEEKDAYS[startDateObj.getDay()]}, ${shortDayMonth(s.localDate)}`;

                  let actionLabel = 'Đăng ký';
                  let actionClass = 'btn sm primary xs';
                  if (hasConfirmed && !past) {
                    actionLabel = 'Chi tiết';
                    actionClass = 'btn sm xs';
                  } else if (hasWaitlist && !past) {
                    actionLabel = 'Xem chờ / Rời';
                    actionClass = 'btn sm xs';
                  } else if (isCancelled || past || isCompleted || !!s.myBookingId) {
                    actionLabel = 'Chi tiết';
                    actionClass = 'btn sm ghost xs';
                  } else if (full && hrs <= waitlistCutoff) {
                    actionLabel = 'Đã kín';
                    actionClass = 'btn sm ghost xs';
                  } else if (full) {
                    actionLabel = 'Vào chờ';
                    actionClass = 'btn sm xs';
                  }

                  return (
                    <tr
                      key={s.id}
                      id={`session-${s.id}`}
                      onClick={() => setSelId(s.id)}
                      style={{
                        cursor: 'pointer',
                        opacity: isCancelled || (past && !hasConfirmed) ? 0.65 : 1,
                      }}
                    >
                      <td>
                        <div className="row" style={{ gap: 6 }}>
                          <span
                            style={{
                              width: 7,
                              height: 7,
                              borderRadius: 2,
                              background: s.color,
                              flexShrink: 0,
                            }}
                          />
                          <b>{s.className}</b>
                        </div>
                        <div style={{ marginTop: 3 }}>
                          <Badge color="purple" plain>
                            {s.category || 'Khác'}
                          </Badge>
                        </div>
                      </td>
                      <td style={{ whiteSpace: 'nowrap' }}>
                        <div className="bold">{dateLabel}</div>
                        <div className="muted" style={{ fontSize: 11.5 }}>
                          {fmtTime(s.startAt)} – {fmtTime(s.endAt)} ({s.durationMinutes}p)
                        </div>
                      </td>
                      <td>
                        <div>
                          {s.room} · {s.branchName}
                        </div>
                        <div className="muted" style={{ fontSize: 11.5 }}>
                          HLV: {s.trainerName}
                        </div>
                      </td>
                      <td style={{ whiteSpace: 'nowrap' }}>
                        <Badge color={full ? 'amber' : 'green'}>
                          {s.bookedCount}/{s.capacity}
                        </Badge>
                        {s.waitlistCount > 0 && (
                          <div className="dim" style={{ fontSize: 11, marginTop: 2 }}>
                            +{s.waitlistCount} chờ
                          </div>
                        )}
                      </td>
                      <td style={{ whiteSpace: 'nowrap' }}>
                        {isCancelled ? (
                          <Badge color="red">Đã hủy</Badge>
                        ) : s.myBookingStatus === 'Attended' ? (
                          <Badge color="blue">Đã tham gia</Badge>
                        ) : hasWaitlist ? (
                          <Badge color="amber">Chờ #{s.myWaitlistPosition ?? ''}</Badge>
                        ) : s.myBookingId ? (
                          <Badge color="green">✓ Đã đặt</Badge>
                        ) : past || isCompleted ? (
                          <Badge color="gray">Đã qua</Badge>
                        ) : full ? (
                          <Badge color="amber">Kín chỗ</Badge>
                        ) : (
                          <Badge color="green">Còn {s.availableSlots} chỗ</Badge>
                        )}
                      </td>
                      <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                        <button
                          type="button"
                          className={actionClass}
                          onClick={(e) => {
                            e.stopPropagation();
                            setSelId(s.id);
                          }}
                          id={`session-action-${s.id}`}
                        >
                          {actionLabel}
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {selectedSession && (
        <SessionModal
          session={selectedSession}
          apiMode={apiMode}
          onClose={() => setSelId(null)}
          onChanged={async () => {
            if (apiMode) {
              await loadFromApi();
            } else {
              setNowMs(Date.now());
            }
          }}
          onLocalBook={() => act((d) => L.bookClass(d, user!.id, selectedSession.id))}
        />
      )}
    </>
  );
}

function SessionModal({
  session,
  apiMode,
  onClose,
  onChanged,
  onLocalBook,
}: {
  session: SessionCardVM;
  apiMode: boolean;
  onClose: () => void;
  onChanged: () => Promise<void>;
  onLocalBook: () => void;
}) {
  const { db, user, act, toast } = useApp();
  const [detail, setDetail] = useState<ApiClassSessionDetailDto | null>(null);
  const [busy, setBusy] = useState(false);

  const fetchDetail = useCallback(async () => {
    if (!apiMode) return;
    try {
      const d = await classesApi.getSessionDetail(session.id);
      setDetail(d);
    } catch {
      /* giữ thông tin summary nếu lỗi */
    }
  }, [apiMode, session.id]);

  useEffect(() => {
    fetchDetail();
  }, [fetchDetail]);

  const s = detail ?? session;
  const full = s.bookedCount >= s.capacity;
  const h = hoursUntil(s.startAt);
  const past = h <= 0;
  const isCancelled = s.status === 'CANCELLED';
  const cancelDeadlineHours = detail?.cancelDeadlineHours ?? db.settings.cancelDeadlineHours;
  const waitlistCutoffHours = detail?.waitlistCutoffHours ?? db.settings.waitlistCutoffHours;

  const hasConfirmedBooking = !!s.myBookingId && s.myBookingStatus === 'Confirmed';
  const hasWaitlist = !!s.myWaitlistEntryId;
  const hasFinishedStatus = !!s.myBookingId && !hasConfirmedBooking;

  const handleBook = async () => {
    if (!apiMode) {
      onLocalBook();
      return;
    }
    if (!getAuthToken()) {
      toast('Vui lòng đăng nhập lại bằng tài khoản Hội viên để đặt lớp.', 'error');
      return;
    }
    setBusy(true);
    try {
      const res = await bookingsApi.bookClass(s.id);
      toast(res.message, res.outcome === 'WAITLISTED' ? 'warning' : 'success');
      await fetchDetail();
      await onChanged();
      emitAppDataChanged();
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setBusy(false);
    }
  };

  const handleCancel = async () => {
    if (!apiMode) {
      const targetId = s.myBookingId || s.myWaitlistEntryId;
      if (targetId) {
        act((d) => L.cancelClassBooking(d, user!.id, targetId));
      }
      onClose();
      return;
    }
    if (!getAuthToken()) {
      toast('Vui lòng đăng nhập lại để hủy lịch.', 'error');
      return;
    }
    setBusy(true);
    try {
      if (hasWaitlist && s.myWaitlistEntryId) {
        await bookingsApi.cancelWaitlist(s.myWaitlistEntryId);
        toast('Đã rời khỏi danh sách chờ.', 'info');
      } else if (s.myBookingId) {
        const res = await bookingsApi.cancelClassBooking(s.myBookingId);
        toast(res.message, res.quotaRefunded ? 'success' : 'warning');
      }
      await onChanged();
      emitAppDataChanged();
      onClose();
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal title={s.className} onClose={onClose}>
      <div className="row wrap mb16">
        <Badge color="purple">{s.category}</Badge>
        {isCancelled && <Badge color="red">Đã hủy</Badge>}
        {s.status === 'COMPLETED' && <Badge color="blue">Đã hoàn thành</Badge>}
        {s.reviewCount > 0 && (
          <span className="row small">
            <Stars value={s.averageRating} size={14} /> {s.averageRating.toFixed(1)} ({s.reviewCount})
          </span>
        )}
      </div>

      <div className="mt16">
        <div className="kv">
          <span>
            <Clock size={14} /> Thời gian
          </span>
          <span>
            {fmtWeekday(s.startAt)}, {fmtDateTime(s.startAt)} · {s.durationMinutes} phút
          </span>
        </div>
        <div className="kv">
          <span>
            <MapPin size={14} /> Địa điểm
          </span>
          <span>
            {s.room} · {s.branchName}
          </span>
        </div>
        <div className="kv">
          <span>
            <Dumbbell size={14} /> Huấn luyện viên
          </span>
          <span>{s.trainerName}</span>
        </div>
        <div className="kv">
          <span>
            <Users size={14} /> Đã đăng ký
          </span>
          <span>
            {s.bookedCount}/{s.capacity} {s.waitlistCount > 0 && `· ${s.waitlistCount} người chờ`}
          </span>
        </div>
      </div>

      <div className="mt8">
        <Progress value={(s.bookedCount / Math.max(1, s.capacity)) * 100} color={full ? 'linear-gradient(90deg,#dc2626,#ea580c)' : undefined} />
      </div>

      {hasWaitlist && (
        <div className="card mt16" style={{ background: 'rgba(217, 119, 6, 0.08)' }}>
          <div className="row">
            <ListOrdered size={18} color="var(--amber)" />
            <b>Bạn đang ở vị trí #{s.myWaitlistPosition ?? '?'} trong danh sách chờ</b>
          </div>
          <div className="small muted mt8">
            Thứ tự dựa trên mức ưu tiên hạng gói và thời điểm đăng ký. Hệ thống tự xếp chỗ khi có người hủy; nếu đến{' '}
            {waitlistCutoffHours} giờ trước giờ học vẫn chưa có chỗ, hàng chờ sẽ tự đóng và thông báo bạn chọn lớp khác.
          </div>
        </div>
      )}

      {!hasWaitlist && full && !hasConfirmedBooking && !past && !isCancelled && h <= waitlistCutoffHours && (
        <div className="card mt16 small" style={{ background: 'rgba(239, 68, 68, 0.08)', color: 'var(--red)' }}>
          Lớp đã đủ sĩ số và chỉ còn dưới {waitlistCutoffHours} giờ trước giờ học nên danh sách chờ đã đóng.
        </div>
      )}

      <div className="row mt24" style={{ justifyContent: 'flex-end' }}>
        {hasConfirmedBooking || hasWaitlist ? (
          <>
            <span className="small muted grow">
              {hasConfirmedBooking &&
                (h >= cancelDeadlineHours
                  ? `Hủy trước ${cancelDeadlineHours}h để được hoàn lượt đặt.`
                  : `⚠️ Đã quá hạn hủy (<${cancelDeadlineHours}h) – hủy sẽ vẫn tính lượt.`)}
            </span>
            <button className="btn danger" disabled={busy || past} onClick={handleCancel} id="cancel-class">
              {busy ? 'Đang xử lý...' : hasWaitlist ? 'Rời danh sách chờ' : 'Hủy đăng ký'}
            </button>
          </>
        ) : hasFinishedStatus ? (
          <Badge color={s.myBookingStatus === 'Attended' ? 'blue' : 'red'}>
            {s.myBookingStatus === 'Attended' ? 'Đã tham gia' : s.myBookingStatus}
          </Badge>
        ) : (
          <button
            className="btn primary"
            disabled={busy || past || isCancelled || s.status === 'COMPLETED' || (full && h <= waitlistCutoffHours)}
            onClick={handleBook}
            id="book-class"
          >
            {busy
              ? 'Đang xử lý...'
              : past
                ? 'Đã diễn ra'
                : isCancelled
                  ? 'Buổi đã hủy'
                  : full
                    ? 'Vào danh sách chờ'
                    : 'Đặt chỗ ngay'}
          </button>
        )}
      </div>
    </Modal>
  );
}

/* ================= Đặt lịch PT ================= */
const PT_GOAL_SUGGESTIONS = [
  'Đốt mỡ vùng bụng & HIIT Cardio',
  'Tăng cơ thân trên (Ngực - Vai - Tay)',
  'Kiểm tra form Squat & Deadlift an toàn',
  'Tập cơ trọng tâm (Core) & Giãn cơ phục hồi',
];

export function MemberPT() {
  const { db, user, act, toast } = useApp();
  const localTrainers = db.users.filter((u) => u.role === 'trainer' && !u.locked);

  const [apiMode, setApiMode] = useState<boolean>(true);
  const [apiTrainers, setApiTrainers] = useState<TrainerItem[]>([]);
  const [apiBranches, setApiBranches] = useState<BranchApiDto[]>([]);
  const [apiTrainerSlots, setApiTrainerSlots] = useState<TrainerAvailabilityItem[]>([]);
  const [loadingSlots, setLoadingSlots] = useState<boolean>(false);
  const [bookingBusy, setBookingBusy] = useState<boolean>(false);

  const [searchTrainer, setSearchTrainer] = useState('');
  const [selectedSpecialty, setSelectedSpecialty] = useState('all');
  const [selectedBranch, setSelectedBranch] = useState('all');
  const [tid, setTid] = useState<string>(localTrainers[0]?.id ?? '');
  const [slot, setSlot] = useState<string | null>(null);
  const [note, setNote] = useState('');
  const [timeOfDay, setTimeOfDay] = useState<'all' | 'morning' | 'afternoon' | 'evening'>('all');
  const [selectedDate, setSelectedDate] = useState<string>('all');
  const [apiBalance, setApiBalance] = useState<ApiPtBalanceDto | null>(null);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      trainerService.getAllTrainers(),
      branchesApi.getAll().catch(() => [] as BranchApiDto[]),
      getAuthToken() ? ptApi.getPtBalance().catch(() => null) : Promise.resolve(null),
    ])
      .then(([trList, brList, balRes]) => {
        if (cancelled) return;
        setApiTrainers(trList);
        setApiBranches(brList);
        if (balRes) setApiBalance(balRes);
        if (trList.length > 0) {
          setTid((prev) => (trList.some((x) => x.id === prev) ? prev : trList[0].id));
        }
        setApiMode(true);
      })
      .catch((err) => {
        if (cancelled) return;
        if (isApiNetworkError(err)) setApiMode(false);
        else toast((err as Error).message, 'error');
      });
    return () => {
      cancelled = true;
    };
  }, [toast]);

  const loadTrainerSlots = useCallback(async (trainerId: string) => {
    if (!apiMode || !trainerId) return;
    setLoadingSlots(true);
    try {
      const slots = await trainerService.getAvailability(trainerId);
      setApiTrainerSlots(slots);
    } catch (err) {
      if (!isApiNetworkError(err)) toast((err as Error).message, 'error');
    } finally {
      setLoadingSlots(false);
    }
  }, [apiMode, toast]);

  useEffect(() => {
    if (apiMode && tid && apiTrainers.some((x) => x.id === tid)) {
      loadTrainerSlots(tid);
    }
  }, [apiMode, tid, apiTrainers, loadTrainerSlots]);

  const localBal = L.ptBalance(db, user!.id);
  const bal = apiBalance
    ? { total: apiBalance.total, free: apiBalance.freeSessions, pkg: apiBalance.packageSessions }
    : localBal;
  const now = Date.now();

  const specialties = useMemo(() => {
    const set = new Set<string>();
    if (apiMode) {
      apiTrainers.forEach((t) =>
        (t.specialty || '')
          .split(',')
          .map((s) => s.trim())
          .filter(Boolean)
          .forEach((s) => set.add(s)),
      );
    } else {
      localTrainers.forEach((t) => t.trainer?.specialties.forEach((s) => set.add(s)));
    }
    return Array.from(set);
  }, [apiMode, apiTrainers, localTrainers]);

  const filteredApiTrainers = useMemo(() => {
    return apiTrainers.filter((t) => {
      if (searchTrainer.trim() && !t.fullName.toLowerCase().includes(searchTrainer.toLowerCase().trim())) return false;
      const specs = (t.specialty || '').split(',').map((s) => s.trim());
      if (selectedSpecialty !== 'all' && !specs.includes(selectedSpecialty)) return false;
      if (selectedBranch !== 'all' && t.branchId !== selectedBranch) return false;
      return true;
    });
  }, [apiTrainers, searchTrainer, selectedSpecialty, selectedBranch]);

  const filteredLocalTrainers = useMemo(() => {
    return localTrainers.filter((t) => {
      if (searchTrainer.trim() && !t.fullName.toLowerCase().includes(searchTrainer.toLowerCase().trim())) return false;
      if (selectedSpecialty !== 'all' && !t.trainer?.specialties.includes(selectedSpecialty)) return false;
      if (selectedBranch !== 'all' && !t.trainer?.branchIds.includes(selectedBranch)) return false;
      return true;
    });
  }, [localTrainers, searchTrainer, selectedSpecialty, selectedBranch]);

  const selectedApiTrainer = apiMode ? apiTrainers.find((x) => x.id === tid) ?? null : null;
  const selectedLocalTrainer = !apiMode ? L.userById(db, tid) : undefined;

  type UnifiedSlot = { id: string; startAt: string; endAt: string; branchId: string; branchName: string; isBooked: boolean };
  const rawSlots: UnifiedSlot[] = useMemo(() => {
    if (apiMode) {
      return apiTrainerSlots
        .filter((s) => new Date(s.startAt).getTime() > now)
        .map((s) => ({
          id: s.id,
          startAt: s.startAt,
          endAt: s.endAt,
          branchId: selectedApiTrainer?.branchId ?? '',
          branchName: selectedApiTrainer?.branchName ?? 'Chi nhánh OOAD',
          isBooked: s.isBooked,
        }))
        .sort((a, b) => a.startAt.localeCompare(b.startAt));
    }
    return db.trainerSlots
      .filter((s) => s.trainerId === tid && new Date(s.startAt).getTime() > now)
      .map((s) => ({
        id: s.id,
        startAt: s.startAt,
        endAt: s.endAt,
        branchId: s.branchId,
        branchName: L.branchById(db, s.branchId)?.name ?? 'Chi nhánh',
        isBooked: L.slotTaken(db, s.id),
      }))
      .sort((a, b) => a.startAt.localeCompare(b.startAt));
  }, [apiMode, apiTrainerSlots, selectedApiTrainer, db, tid, now]);

  const availableDates = useMemo(() => {
    return Array.from(new Set(rawSlots.map((s) => toDateStr(new Date(s.startAt)))));
  }, [rawSlots]);

  const slots = useMemo(() => {
    return rawSlots.filter((s) => {
      const d = toDateStr(new Date(s.startAt));
      if (selectedDate !== 'all' && d !== selectedDate) return false;

      const hour = new Date(s.startAt).getHours();
      if (timeOfDay === 'morning' && hour >= 12) return false;
      if (timeOfDay === 'afternoon' && (hour < 12 || hour >= 17)) return false;
      if (timeOfDay === 'evening' && hour < 17) return false;

      return true;
    });
  }, [rawSlots, selectedDate, timeOfDay]);

  const byDay = useMemo(() => {
    return slots.reduce<Record<string, UnifiedSlot[]>>((acc, s) => {
      const d = toDateStr(new Date(s.startAt));
      (acc[d] ||= []).push(s);
      return acc;
    }, {});
  }, [slots]);

  const rating = (id: string) => {
    const rs = db.reviews.filter((r) => r.trainerId === id);
    return { avg: rs.length ? rs.reduce((a, r) => a + r.rating, 0) / rs.length : 0, n: rs.length };
  };

  const book = async () => {
    if (!slot) return;
    if (apiMode) {
      if (!getAuthToken()) {
        toast('Vui lòng đăng nhập bằng tài khoản Hội viên để đặt lịch PT.', 'error');
        return;
      }
      setBookingBusy(true);
      try {
        await ptApi.bookPt({ availabilityId: slot, note });
        toast('Đã gửi yêu cầu đặt lịch đến HLV thành công!', 'success');
        setSlot(null);
        setNote('');
        await Promise.all([
          ptApi.getPtBalance().then(setApiBalance).catch(() => {}),
          loadTrainerSlots(tid),
        ]);
        emitAppDataChanged();
      } catch (err) {
        if (isApiNetworkError(err)) {
          const r = act((d) => L.bookPT(d, user!.id, slot, note), 'Đã gửi yêu cầu đặt lịch, chờ HLV xác nhận');
          if (r) {
            setSlot(null);
            setNote('');
          }
        } else {
          toast((err as Error).message, 'error');
        }
      } finally {
        setBookingBusy(false);
      }
      return;
    }

    const r = act((d) => L.bookPT(d, user!.id, slot, note), 'Đã gửi yêu cầu đặt lịch, chờ HLV xác nhận');
    if (r) {
      setSlot(null);
      setNote('');
    }
  };

  const sel = rawSlots.find((s) => s.id === slot);
  const displayedCount = apiMode ? filteredApiTrainers.length : filteredLocalTrainers.length;
  const activeTrainerName = apiMode ? selectedApiTrainer?.fullName : selectedLocalTrainer?.fullName;

  return (
    <>
      <PageHead
        title="Đặt Lịch HLV Cá Nhân (PT 1-kèm-1)"
        desc="Quy trình 3 bước rõ ràng: Chọn HLV chuyên môn → Chọn Ca rảnh theo buổi → Chọn mục tiêu tập & Xác nhận."
        actions={
          <div className="card row" style={{ padding: '8px 16px', gap: 16 }}>
            <span className="row small">
              <Gift size={16} color="var(--primary)" /> Tặng kèm: <b style={{ color: 'var(--primary)' }}>{bal.free} buổi</b>
            </span>
            <span className="row small">
              <Dumbbell size={16} color="var(--accent)" /> Gói PT: <b style={{ color: 'var(--accent)' }}>{bal.pkg} buổi</b>
            </span>
          </div>
        }
      />

      {/* Thanh quy trình 3 bước */}
      <div className="grid g3 mb16" style={{ gap: 12 }}>
        <div className="card row" style={{ padding: '12px 16px', borderLeft: '4px solid var(--primary)' }}>
          <Badge color="purple">Bước 1</Badge>
          <div className="small">
            <div className="bold">Chọn Huấn luyện viên</div>
            <div className="dim">Đang chọn: {activeTrainerName || 'Chưa chọn'}</div>
          </div>
        </div>
        <div className="card row" style={{ padding: '12px 16px', borderLeft: `4px solid ${sel ? 'var(--accent)' : 'var(--border)'}` }}>
          <Badge color={sel ? 'green' : 'gray'}>Bước 2</Badge>
          <div className="small">
            <div className="bold">Chọn Ngày & Khung giờ rảnh</div>
            <div className="dim">{sel ? `${fmtDate(sel.startAt)} · ${fmtTime(sel.startAt)}` : 'Chọn 1 ca trống bên dưới'}</div>
          </div>
        </div>
        <div className="card row" style={{ padding: '12px 16px', borderLeft: `4px solid ${note ? 'var(--amber)' : 'var(--border)'}` }}>
          <Badge color={note ? 'amber' : 'gray'}>Bước 3</Badge>
          <div className="small">
            <div className="bold">Mục tiêu & Xác nhận đặt</div>
            <div className="dim">Đặt trước tối thiểu {db.settings.cancelDeadlineHours}h</div>
          </div>
        </div>
      </div>

      {/* THANH BỘ LỌC HUẤN LUYỆN VIÊN */}
      <div className="card mb16" style={{ padding: 14 }}>
        <div className="row wrap between" style={{ gap: 12 }}>
          <div className="row wrap grow" style={{ gap: 10 }}>
            <div style={{ position: 'relative', minWidth: 220 }}>
              <Search size={15} className="dim" style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)' }} />
              <input
                className="input sm"
                style={{ paddingLeft: 32, width: '100%' }}
                placeholder="Tìm theo tên HLV..."
                value={searchTrainer}
                onChange={(e) => setSearchTrainer(e.target.value)}
              />
            </div>

            <select
              className="input sm"
              style={{ minWidth: 160 }}
              value={selectedSpecialty}
              onChange={(e) => setSelectedSpecialty(e.target.value)}
            >
              <option value="all">Tất cả chuyên môn</option>
              {specialties.map((s) => (
                <option key={s} value={s}>
                  {s}
                </option>
              ))}
            </select>

            <select
              className="input sm"
              style={{ minWidth: 170 }}
              value={selectedBranch}
              onChange={(e) => setSelectedBranch(e.target.value)}
            >
              <option value="all">Tất cả cơ sở TP.HCM</option>
              {(apiMode ? apiBranches : db.branches).map((b) => (
                <option key={b.id} value={b.id}>
                  {b.name}
                </option>
              ))}
            </select>
          </div>

          <span className="small muted">
            Hiển thị <b>{displayedCount}</b> huấn luyện viên
          </span>
        </div>
      </div>

      <div className="grid" style={{ gridTemplateColumns: 'minmax(280px, 330px) 1fr', gap: 18 }}>
        {/* DANH SÁCH HLV */}
        <div className="col" style={{ gap: 10 }}>
          {displayedCount === 0 ? (
            <div className="card" style={{ padding: 24, textAlign: 'center' }}>
              <Empty text="Không tìm thấy HLV phù hợp bộ lọc." />
              <button
                className="btn sm ghost mt8"
                onClick={() => {
                  setSearchTrainer('');
                  setSelectedSpecialty('all');
                  setSelectedBranch('all');
                }}
              >
                Đặt lại bộ lọc
              </button>
            </div>
          ) : apiMode ? (
            filteredApiTrainers.map((t) => {
              const isSel = t.id === tid;
              return (
                <div
                  key={t.id}
                  className={`card hover ${isSel ? 'border-primary' : ''}`}
                  style={{
                    padding: 13,
                    cursor: 'pointer',
                    background: isSel ? 'rgba(79, 70, 229, 0.08)' : undefined,
                    border: isSel ? '2px solid var(--primary)' : '1px solid var(--border)',
                    transition: 'all 0.2s ease',
                  }}
                  onClick={() => {
                    setTid(t.id);
                    setSlot(null);
                    setSelectedDate('all');
                  }}
                  id={`trainer-${t.id}`}
                >
                  <div className="row" style={{ gap: 12, alignItems: 'center' }}>
                    <Avatar user={{ fullName: t.fullName, avatarColor: '#7c5cff' }} />
                    <div className="grow" style={{ minWidth: 0 }}>
                      <div className="row between">
                        <span className="bold" style={{ fontSize: 15, color: isSel ? 'var(--primary)' : 'var(--text)' }}>
                          {t.fullName}
                        </span>
                        <Badge color="purple" plain>{t.branchName?.replace('DF Gym Center - ', '') || 'PT'}</Badge>
                      </div>
                      <div className="small dim truncate" style={{ marginTop: 2 }}>
                        {t.specialty || 'Huấn luyện viên cá nhân'}
                      </div>
                      <div className="row between small mt8" style={{ alignItems: 'center' }}>
                        <div className="row" style={{ gap: 4 }}>
                          <Stars value={t.averageRating || 5} size={13} />
                          <span className="bold">{t.averageRating ? t.averageRating.toFixed(1) : '5.0'}</span>
                          <span className="dim">({t.reviewCount})</span>
                        </div>
                        <span className="dim">{t.experienceYears} năm KN</span>
                      </div>
                    </div>
                  </div>
                </div>
              );
            })
          ) : (
            filteredLocalTrainers.map((t) => {
              const r = rating(t.id);
              const isSel = t.id === tid;
              const openSlotsCount = db.trainerSlots.filter(
                (s) => s.trainerId === t.id && new Date(s.startAt).getTime() > now && !L.slotTaken(db, s.id),
              ).length;

              return (
                <div
                  key={t.id}
                  className={`card hover ${isSel ? 'border-primary' : ''}`}
                  style={{
                    padding: 13,
                    cursor: 'pointer',
                    background: isSel ? 'rgba(79, 70, 229, 0.08)' : undefined,
                    border: isSel ? '2px solid var(--primary)' : '1px solid var(--border)',
                    transition: 'all 0.2s ease',
                  }}
                  onClick={() => {
                    setTid(t.id);
                    setSlot(null);
                    setSelectedDate('all');
                  }}
                  id={`trainer-${t.id}`}
                >
                  <div className="row" style={{ gap: 12, alignItems: 'center' }}>
                    <Avatar user={t} />
                    <div className="grow" style={{ minWidth: 0 }}>
                      <div className="row between">
                        <span className="bold" style={{ fontSize: 15, color: isSel ? 'var(--primary)' : 'var(--text)' }}>
                          {t.fullName}
                        </span>
                        {openSlotsCount > 0 ? (
                          <Badge color="green" plain>
                            {openSlotsCount} ca trống
                          </Badge>
                        ) : (
                          <span className="small dim">Hết ca</span>
                        )}
                      </div>
                      <div className="small dim truncate" style={{ marginTop: 2 }}>
                        {t.trainer?.specialties.join(' · ')}
                      </div>
                      <div className="row between small mt8" style={{ alignItems: 'center' }}>
                        <div className="row" style={{ gap: 4 }}>
                          <Stars value={r.avg} size={13} />
                          <span className="bold">{r.avg ? r.avg.toFixed(1) : '5.0'}</span>
                          <span className="dim">({r.n})</span>
                        </div>
                        <span className="dim">{t.trainer?.experienceYears} năm KN</span>
                      </div>
                    </div>
                  </div>
                </div>
              );
            })
          )}
        </div>

        {/* CHI TIẾT HLV & CHỌN LỊCH RẢNH */}
        {(selectedApiTrainer || selectedLocalTrainer) && (
          <div className="col" style={{ gap: 18 }}>
            <div className="card" style={{ padding: 20 }}>
              <div className="row" style={{ gap: 18, alignItems: 'flex-start' }}>
                <Avatar
                  user={
                    selectedApiTrainer
                      ? { fullName: selectedApiTrainer.fullName, avatarColor: '#7c5cff' }
                      : selectedLocalTrainer
                  }
                  size="lg"
                />
                <div className="grow">
                  <div className="row between wrap" style={{ gap: 8 }}>
                    <div>
                      <h2 style={{ fontSize: 20, marginBottom: 4 }}>HLV {activeTrainerName}</h2>
                      <div className="row wrap" style={{ gap: 6 }}>
                        {(selectedApiTrainer
                          ? (selectedApiTrainer.specialty || 'Fitness').split(',').map((s) => s.trim()).filter(Boolean)
                          : selectedLocalTrainer?.trainer?.specialties ?? []
                        ).map((s) => (
                          <Badge key={s} color="purple">
                            {s}
                          </Badge>
                        ))}
                      </div>
                    </div>
                    <div
                      className="row small"
                      style={{ gap: 6, background: 'var(--surface-2)', padding: '6px 12px', borderRadius: 8 }}
                    >
                      {selectedApiTrainer ? (
                        <>
                          <Stars value={selectedApiTrainer.averageRating || 5} size={15} />
                          <span className="bold">{(selectedApiTrainer.averageRating || 5).toFixed(1)}</span>
                          <span className="dim">({selectedApiTrainer.reviewCount} đánh giá)</span>
                        </>
                      ) : (
                        <>
                          <Stars value={rating(selectedLocalTrainer!.id).avg} size={15} />
                          <span className="bold">{rating(selectedLocalTrainer!.id).avg ? rating(selectedLocalTrainer!.id).avg.toFixed(1) : '5.0'}</span>
                          <span className="dim">({rating(selectedLocalTrainer!.id).n} đánh giá)</span>
                        </>
                      )}
                    </div>
                  </div>

                  <p className="muted mt8" style={{ lineHeight: 1.55 }}>
                    {(selectedApiTrainer ? selectedApiTrainer.bio : selectedLocalTrainer?.trainer?.bio) ||
                      'Huấn luyện viên tận tâm, kinh nghiệm xây dựng giáo án cá nhân hóa đạt chuẩn quốc tế.'}
                  </p>

                  <div className="row wrap small muted mt12" style={{ gap: 16 }}>
                    <span className="row" style={{ gap: 6 }}>
                      <Award size={15} color="var(--primary)" />
                      <b>{selectedApiTrainer ? selectedApiTrainer.experienceYears : selectedLocalTrainer?.trainer?.experienceYears} năm</b> kinh nghiệm
                    </span>
                    {selectedLocalTrainer?.trainer?.certifications && (
                      <span className="row" style={{ gap: 6 }}>
                        <CheckCircle2 size={15} color="var(--teal)" />
                        {selectedLocalTrainer.trainer.certifications.join(', ')}
                      </span>
                    )}
                    <span className="row" style={{ gap: 6 }}>
                      <MapPin size={15} color="var(--amber)" />
                      {selectedApiTrainer
                        ? (selectedApiTrainer.branchName || 'Hệ thống DFGymCenter')
                        : selectedLocalTrainer?.trainer?.branchIds.map((b) => L.branchById(db, b)?.name).join(', ')}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <div className="card">
              <div className="card-head between wrap" style={{ gap: 12 }}>
                <div>
                  <h2>
                    <CalendarDays size={18} /> Lịch Rảnh Của HLV {activeTrainerName}
                  </h2>
                  <span className="small muted">
                    Chỉ các khung giờ cách hiện tại tối thiểu {db.settings.cancelDeadlineHours} giờ mới có thể đặt lịch
                  </span>
                </div>

                <div className="chips">
                  <button className={`chip ${timeOfDay === 'all' ? 'on' : ''}`} onClick={() => setTimeOfDay('all')}>
                    Tất cả buổi
                  </button>
                  <button className={`chip ${timeOfDay === 'morning' ? 'on' : ''}`} onClick={() => setTimeOfDay('morning')}>
                    <Sunrise size={13} style={{ marginRight: 4 }} /> Sáng (5h30–12h)
                  </button>
                  <button className={`chip ${timeOfDay === 'afternoon' ? 'on' : ''}`} onClick={() => setTimeOfDay('afternoon')}>
                    <Sun size={13} style={{ marginRight: 4 }} /> Chiều (12h–17h)
                  </button>
                  <button className={`chip ${timeOfDay === 'evening' ? 'on' : ''}`} onClick={() => setTimeOfDay('evening')}>
                    <Sunset size={13} style={{ marginRight: 4 }} /> Tối (17h–22h)
                  </button>
                </div>
              </div>

              {availableDates.length > 1 && (
                <div className="row wrap pb8 mb16" style={{ gap: 8, borderBottom: '1px solid var(--border)' }}>
                  <button
                    className={`btn sm ${selectedDate === 'all' ? 'primary' : 'ghost'}`}
                    onClick={() => setSelectedDate('all')}
                  >
                    Tất cả ngày ({rawSlots.length})
                  </button>
                  {availableDates.map((d) => {
                    const count = rawSlots.filter((s) => toDateStr(new Date(s.startAt)) === d).length;
                    const isCur = selectedDate === d;
                    return (
                      <button
                        key={d}
                        className={`btn sm ${isCur ? 'primary' : 'ghost'}`}
                        onClick={() => setSelectedDate(d)}
                      >
                        {fmtWeekday(d)}, {fmtDate(d).slice(0, 5)} ({count})
                      </button>
                    );
                  })}
                </div>
              )}

              {loadingSlots ? (
                <div style={{ padding: '24px', textAlign: 'center' }} className="muted">Đang tải khung giờ rảnh của HLV...</div>
              ) : slots.length === 0 ? (
                <div style={{ padding: '30px 20px', textAlign: 'center' }}>
                  <Empty text="Không có khung giờ rảnh nào phù hợp với bộ lọc buổi hoặc ngày này." />
                </div>
              ) : (
                <div className="col" style={{ gap: 18 }}>
                  {Object.entries(byDay).map(([d, list]) => (
                    <div key={d}>
                      <div className="period-header mb8">
                        <span className="bold" style={{ fontSize: 13.5, color: 'var(--primary)' }}>
                          📅 {fmtWeekday(d)}, ngày {fmtDate(d)}
                        </span>
                        <span className="small dim">{list.length} khung giờ</span>
                      </div>

                      <div className="grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(185px, 1fr))', gap: 10 }}>
                        {list.map((s) => {
                          const taken = s.isBooked;
                          const tooSoon = hoursUntil(s.startAt) < db.settings.cancelDeadlineHours;
                          const disabled = taken || tooSoon;
                          const isSel = slot === s.id;
                          const startMs = new Date(s.startAt).getTime();
                          const endMs = new Date(s.endAt).getTime();
                          const durMin = Math.round((endMs - startMs) / 60000);
                          const pKey = getPeriodKey(s.startAt);
                          const pLabel = pKey === 'morning' ? 'Sáng' : pKey === 'afternoon' ? 'Chiều' : 'Tối';

                          return (
                            <button
                              key={s.id}
                              className={`card hover ${isSel ? 'border-primary' : ''}`}
                              disabled={disabled}
                              onClick={() => setSlot(s.id)}
                              style={{
                                padding: 13,
                                textAlign: 'left',
                                cursor: disabled ? 'not-allowed' : 'pointer',
                                background: isSel
                                  ? 'rgba(79, 70, 229, 0.1)'
                                  : disabled
                                    ? 'var(--surface-2)'
                                    : 'var(--surface)',
                                border: isSel ? '2px solid var(--primary)' : '1px solid var(--border)',
                                opacity: disabled ? 0.52 : 1,
                              }}
                              id={`slot-${s.id}`}
                            >
                              <div className="row between mb4">
                                <span
                                  style={{
                                    fontWeight: 800,
                                    fontSize: 14.5,
                                    color: isSel ? 'var(--primary)' : 'var(--text)',
                                  }}
                                >
                                  {fmtTime(s.startAt)} – {fmtTime(s.endAt)}
                                </span>
                                <Badge color={durMin === 90 ? 'purple' : durMin === 60 ? 'blue' : 'amber'} plain>
                                  {durMin}p
                                </Badge>
                              </div>

                              <div className="small dim truncate">
                                📍 {s.branchName} · Ca {pLabel}
                              </div>

                              <div className="row between mt8 small">
                                {taken ? (
                                  <span style={{ color: 'var(--red)', fontWeight: 600 }}>Đã có người đặt</span>
                                ) : tooSoon ? (
                                  <span style={{ color: 'var(--amber)', fontWeight: 600 }}>Sát giờ (&lt;2h)</span>
                                ) : isSel ? (
                                  <span style={{ color: 'var(--primary)', fontWeight: 700 }}>✓ Đang chọn</span>
                                ) : (
                                  <span style={{ color: 'var(--green)', fontWeight: 600 }}>● Còn trống</span>
                                )}
                              </div>
                            </button>
                          );
                        })}
                      </div>
                    </div>
                  ))}
                </div>
              )}

              {sel && (
                <div
                  className="card mt16"
                  style={{
                    background: 'rgba(79, 70, 229, 0.08)',
                    borderColor: 'var(--primary)',
                    padding: 18,
                  }}
                >
                  <div className="row between wrap mb8" style={{ gap: 8 }}>
                    <div className="bold" style={{ fontSize: 16 }}>
                      Xác nhận ca tập: {fmtWeekday(sel.startAt)}, {fmtDateTime(sel.startAt)} (
                      {Math.round((new Date(sel.endAt).getTime() - new Date(sel.startAt).getTime()) / 60000)} phút)
                    </div>
                    <Badge color="green">{sel.branchName}</Badge>
                  </div>

                  <div className="small muted mb12" style={{ lineHeight: 1.5 }}>
                    📌 Huấn luyện viên: <b>{activeTrainerName}</b>. Sẽ trừ <b>1 buổi</b> (ưu tiên{' '}
                    {bal.free > 0 ? 'buổi tặng kèm của gói hội viên' : 'từ gói PT cá nhân'}). Hủy trước{' '}
                    {db.settings.cancelDeadlineHours} giờ hoặc HLV từ chối lịch sẽ được hoàn lại 100% buổi tập.
                  </div>

                  <div className="mb12">
                    <div className="small bold mb6" style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                      <Sparkles size={14} color="var(--primary)" /> Chọn nhanh mục tiêu buổi tập:
                    </div>
                    <div className="chips">
                      {PT_GOAL_SUGGESTIONS.map((g) => (
                        <button
                          key={g}
                          type="button"
                          className={`chip ${note === g ? 'on' : ''}`}
                          onClick={() => setNote(g)}
                        >
                          {g}
                        </button>
                      ))}
                    </div>
                  </div>

                  <div className="row wrap" style={{ gap: 10 }}>
                    <input
                      className="input grow"
                      placeholder="Ghi chú cho HLV (vd: mục tiêu buổi tập, thể lực, chấn thương nếu có...)"
                      value={note}
                      onChange={(e) => setNote(e.target.value)}
                      id="pt-note"
                    />
                    <button
                      className="btn primary"
                      onClick={book}
                      disabled={bal.total === 0 || bookingBusy}
                      id="pt-book"
                      style={{ minWidth: 150 }}
                    >
                      {bookingBusy ? 'Đang gửi...' : 'Xác nhận đặt lịch'}
                    </button>
                  </div>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </>
  );
}

/* ================= Lịch của tôi ================= */
type Row = {
  id: string;
  rowType: 'booking' | 'waitlist';
  kind: 'class' | 'pt';
  at: string;
  end: string;
  title: string;
  roomOrBranch: string;
  trainerName: string;
  sub: string;
  status: [string, string];
  raw: string;
  canCancel: boolean;
  isCancelTimely: boolean;
  cancelDeadlineHours: number;
  canReview: boolean;
  reviewed: boolean;
  source?: string;
};

function mapBackendBookingStatus(status: string, kind: 'class' | 'pt'): [string, string] {
  switch (status) {
    case 'Confirmed':
      return [kind === 'pt' ? 'Đã xác nhận' : 'Đã đặt', 'green'];
    case 'Pending':
      return ['Chờ HLV xác nhận', 'amber'];
    case 'Cancelled':
      return ['Đã hủy', 'gray'];
    case 'LateCancelled':
      return ['Hủy quá hạn', 'red'];
    case 'AutoCancelled':
      return ['Hủy tự động', 'purple'];
    case 'Attended':
      return [kind === 'pt' ? 'Hoàn thành' : 'Đã tham gia', 'blue'];
    case 'NoShow':
      return ['Vắng mặt', 'red'];
    case 'Rejected':
      return ['Bị từ chối', 'red'];
    default:
      return [status, 'gray'];
  }
}

function normalizeBackendStatusKey(status: string): string {
  switch (status) {
    case 'Confirmed':
      return 'booked';
    case 'Pending':
      return 'pending';
    case 'Cancelled':
      return 'cancelled';
    case 'LateCancelled':
      return 'late_cancelled';
    case 'AutoCancelled':
      return 'auto_cancelled';
    case 'Attended':
      return 'attended';
    case 'NoShow':
      return 'no_show';
    case 'Rejected':
      return 'rejected';
    default:
      return status.toLowerCase();
  }
}

export function MemberBookings() {
  const { db, user, act, toast } = useApp();
  const [tab, setTab] = useState<'up' | 'past'>('up');
  const [kindFilter, setKindFilter] = useState<'all' | 'class' | 'pt'>('all');
  const [layoutMode, setLayoutMode] = useState<'calendar' | 'list'>('calendar');
  const [weekIdx, setWeekIdx] = useState<number>(0);
  const [review, setReview] = useState<Row | null>(null);
  const [cancel, setCancel] = useState<Row | null>(null);
  const [apiMode, setApiMode] = useState<boolean>(!!getAuthToken());
  const [loading, setLoading] = useState<boolean>(false);
  const [apiSchedule, setApiSchedule] = useState<ApiMyScheduleSummaryDto | null>(null);
  const uid = user!.id;

  const loadMySchedule = useCallback(async () => {
    if (!getAuthToken()) {
      setApiMode(false);
      return;
    }
    setLoading(true);
    try {
      const data = await bookingsApi.getMySchedule();
      setApiSchedule(data);
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
    loadMySchedule();
  }, [loadMySchedule]);

  const rows: Row[] = useMemo(() => {
    const now = Date.now();
    if (apiMode && apiSchedule) {
      const bookingRows: Row[] = apiSchedule.bookings.map((b) => {
        const kind: 'class' | 'pt' = b.type === 'PersonalTrainer' ? 'pt' : 'class';
        return {
          id: b.id,
          rowType: 'booking',
          kind,
          at: b.startAt,
          end: b.endAt,
          title: b.title,
          roomOrBranch: b.room ? `${b.room} · ${b.branchName}` : b.branchName,
          trainerName: b.trainerName,
          sub: `${b.room ? b.room + ' · ' : ''}${b.branchName} · HLV ${b.trainerName}`,
          status: mapBackendBookingStatus(b.status, kind),
          raw: normalizeBackendStatusKey(b.status),
          canCancel: b.canCancel,
          isCancelTimely: b.isCancelTimely,
          cancelDeadlineHours: b.cancelDeadlineHours,
          canReview: b.canReview,
          reviewed: b.isReviewed,
        };
      });

      const waitlistRows: Row[] = apiSchedule.waitlistEntries.map((w) => {
        const isWaiting = w.status === 'WAITING';
        const isExpired = w.status === 'EXPIRED';
        return {
          id: w.id,
          rowType: 'waitlist',
          kind: 'class',
          at: w.startAt,
          end: w.endAt,
          title: w.className,
          roomOrBranch: `${w.room} · ${w.branchName}`,
          trainerName: w.trainerName,
          sub: `${w.room} · ${w.branchName} · HLV ${w.trainerName}${isWaiting ? ` · Vị trí chờ #${w.position} (Ưu tiên ${w.priorityLevel})` : ''}`,
          status: isWaiting
            ? [`Chờ #${w.position}`, 'amber']
            : isExpired
              ? ['Lớp đã đầy (T-8h)', 'gray']
              : ['Đã rời chờ', 'gray'],
          raw: isWaiting ? 'waitlist' : isExpired ? 'waitlist_expired' : 'cancelled',
          canCancel: isWaiting && new Date(w.startAt).getTime() > now,
          isCancelTimely: true,
          cancelDeadlineHours: apiSchedule.waitlistCutoffHours,
          canReview: false,
          reviewed: false,
        };
      });

      return [...bookingRows, ...waitlistRows];
    }

    const cls: Row[] = db.classBookings
      .filter((b) => b.memberId === uid)
      .map((b) => {
        const s = L.sessionById(db, b.sessionId)!;
        const c = L.classById(db, s.classId)!;
        const brName = L.branchById(db, c.branchId)?.name ?? '';
        const trName = L.userById(db, s.trainerId)?.fullName ?? '';
        return {
          id: b.id,
          rowType: b.status === 'waitlist' ? 'waitlist' : 'booking',
          kind: 'class',
          at: s.startAt,
          end: s.endAt,
          title: c.name,
          roomOrBranch: `${s.room} · ${brName}`,
          trainerName: trName,
          raw: b.status,
          sub: `${s.room} · ${brName} · HLV ${trName}`,
          status: cbLabel[b.status],
          canCancel: ['booked', 'waitlist'].includes(b.status) && new Date(s.startAt).getTime() > now,
          isCancelTimely: b.status === 'waitlist' || hoursUntil(s.startAt) >= db.settings.cancelDeadlineHours,
          cancelDeadlineHours: db.settings.cancelDeadlineHours,
          canReview: b.status === 'attended',
          reviewed: db.reviews.some((r) => r.bookingId === b.id),
        };
      });
    const pts: Row[] = db.ptBookings
      .filter((b) => b.memberId === uid)
      .map((b) => {
        const brName = L.branchById(db, b.branchId)?.name ?? '';
        const trName = L.userById(db, b.trainerId)?.fullName ?? '';
        return {
          id: b.id,
          rowType: 'booking',
          kind: 'pt',
          at: b.startAt,
          end: b.endAt,
          title: `PT 1-kèm-1 · ${trName}`,
          roomOrBranch: `Khu PT · ${brName}`,
          trainerName: trName,
          raw: b.status,
          sub: `${brName}${b.note ? ' · ' + b.note : ''}`,
          status: ptbLabel[b.status],
          canCancel: ['pending', 'confirmed'].includes(b.status) && new Date(b.startAt).getTime() > now,
          isCancelTimely: b.status === 'pending' || hoursUntil(b.startAt) >= db.settings.cancelDeadlineHours,
          cancelDeadlineHours: db.settings.cancelDeadlineHours,
          canReview: b.status === 'completed',
          reviewed: db.reviews.some((r) => r.bookingId === b.id),
          source: b.source.type === 'membership_free' ? 'Buổi tặng kèm' : 'Gói PT',
        };
      });
    return [...cls, ...pts];
  }, [apiMode, apiSchedule, db, uid]);

  const active = ['booked', 'waitlist', 'pending', 'confirmed'];
  const up = rows
    .filter((r) => active.includes(r.raw) && new Date(r.end).getTime() > Date.now())
    .sort((a, b) => a.at.localeCompare(b.at));
  const past = rows.filter((r) => !up.includes(r)).sort((a, b) => b.at.localeCompare(a.at));
  const baseList = tab === 'up' ? up : past;
  const list = baseList.filter((r) => (kindFilter === 'all' ? true : r.kind === kindFilter));
  const toReview = past.filter((r) => r.canReview && !r.reviewed).length;

  const monday = getMondayStr(todayStr(), weekIdx);
  const weekDays = Array.from({ length: 7 }, (_, i) => addDays(monday, i));

  const doCancel = async () => {
    if (!cancel) return;
    const target = cancel;
    setCancel(null);

    if (apiMode) {
      try {
        if (target.rowType === 'waitlist') {
          await bookingsApi.cancelWaitlist(target.id);
          toast('Đã rời khỏi danh sách chờ.', 'info');
        } else if (target.kind === 'class') {
          const res = await bookingsApi.cancelClassBooking(target.id);
          toast(res.message, res.quotaRefunded ? 'success' : 'warning');
        } else {
          await ptApi.cancelPtBooking(target.id);
          toast('Đã hủy lịch hẹn PT thành công và hoàn lại buổi tập vào gói của bạn.', 'success');
        }
        await loadMySchedule();
        emitAppDataChanged();
      } catch (err) {
        toast((err as Error).message, 'error');
      }
      return;
    }

    act((d) => (target.kind === 'class' ? L.cancelClassBooking(d, uid, target.id) : L.cancelPTByMember(d, uid, target.id)));
  };

  return (
    <>
      <PageHead
        title="Thời Khóa Biểu Cá Nhân Của Tôi"
        desc={
          apiMode && apiSchedule?.activePlanName
            ? `Gói ${apiSchedule.activePlanName} · Đã dùng ${apiSchedule.usedBookingsThisMonth}${apiSchedule.maxBookingsPerMonth > 0 ? `/${apiSchedule.maxBookingsPerMonth}` : ''} lượt lớp trong tháng.`
            : 'Quản lý tập trung toàn bộ lịch Lớp nhóm và buổi PT 1-kèm-1 đã đăng ký theo chuẩn bảng tuần.'
        }
        actions={
          <div className="row wrap" style={{ gap: 8 }}>
            {apiMode && (
              <button className="btn sm" onClick={loadMySchedule} disabled={loading}>
                <RefreshCw size={14} /> {loading ? 'Đang tải...' : 'Làm mới'}
              </button>
            )}
            <div className="tabs" style={{ padding: 3, marginBottom: 0 }}>
              <button
                className={`tab ${layoutMode === 'calendar' ? 'on' : ''}`}
                onClick={() => {
                  setLayoutMode('calendar');
                  setTab('up');
                }}
              >
                <LayoutGrid size={14} style={{ marginRight: 5 }} /> Thời khóa biểu dạng tuần
              </button>
              <button className={`tab ${layoutMode === 'list' ? 'on' : ''}`} onClick={() => setLayoutMode('list')}>
                <List size={14} style={{ marginRight: 5 }} /> Danh sách & Lịch sử ({rows.length})
              </button>
            </div>
          </div>
        }
      />

      {layoutMode === 'calendar' ? (
        <div className="tkb-wrapper">
          <div className="tkb-header-bar">
            <div className="tkb-header-title">
              <Settings size={16} color="var(--primary)" />
              <span>THỜI KHÓA BIỂU CÁ NHÂN DẠNG TUẦN – {user!.fullName.toUpperCase()}</span>
            </div>
            <div className="tkb-legend">
              <span className="tkb-legend-item">
                <span className="tkb-swatch" style={{ background: '#10b981' }} /> Lớp học nhóm ({up.filter((x) => x.kind === 'class').length})
              </span>
              <span className="tkb-legend-item">
                <span className="tkb-swatch" style={{ background: '#a855f7' }} /> PT 1-kèm-1 ({up.filter((x) => x.kind === 'pt').length})
              </span>
            </div>
          </div>

          <div className="tkb-table-scroll">
            <table className="tkb-table">
              <thead>
                <tr>
                  <th className="tkb-nav-col">
                    <button
                      type="button"
                      className="tkb-nav-btn"
                      onClick={() => setWeekIdx((w) => Math.max(-1, w - 1))}
                      disabled={weekIdx <= -1}
                    >
                      <ArrowLeft size={15} />
                    </button>
                  </th>
                  {weekDays.map((d) => {
                    const isToday = d === todayStr();
                    return (
                      <th key={d} className={isToday ? 'is-today' : ''}>
                        <span>{fullDayName(d)}</span>
                        <span className="tkb-day-sub">({shortDayMonth(d)})</span>
                      </th>
                    );
                  })}
                  <th className="tkb-nav-col">
                    <button
                      type="button"
                      className="tkb-nav-btn"
                      onClick={() => setWeekIdx((w) => Math.min(1, w + 1))}
                      disabled={weekIdx >= 1}
                    >
                      <ArrowRight size={15} />
                    </button>
                  </th>
                </tr>
              </thead>
              <tbody>
                {GYM_TIME_SLOTS.map((slotDef) => (
                  <tr key={slotDef.id}>
                    <td className="tkb-slot-cell">
                      <div className="tkb-slot-badge">{slotDef.label}</div>
                    </td>
                    {weekDays.map((d) => {
                      const isToday = d === todayStr();
                      const cellItems = rows.filter(
                        (r) =>
                          !['cancelled', 'late_cancelled', 'auto_cancelled', 'rejected', 'waitlist_expired'].includes(r.raw) &&
                          toDateStr(new Date(r.at)) === d &&
                          matchSlotId(r.at) === slotDef.id,
                      );
                      return (
                        <td key={`${slotDef.id}-${d}`} className={`tkb-data-cell ${isToday ? 'is-today-col' : ''}`}>
                          {cellItems.map((item) => (
                            <div
                              key={item.id}
                              className={`tkb-event ${item.kind === 'pt' ? 'is-pt' : item.raw === 'waitlist' ? 'is-full' : 'is-mine'}`}
                            >
                              <div className="tkb-ev-title">{item.title}</div>
                              <div className="tkb-ev-line">{item.roomOrBranch}</div>
                              <div className="tkb-ev-line">HLV: {item.trainerName}</div>
                              <div className="tkb-ev-time">
                                <span>
                                  🕒 {fmtTime(item.at)} → {fmtTime(item.end)}
                                </span>
                                <span className="tkb-ev-tag">{item.status[0]}</span>
                              </div>
                              {item.canCancel && (
                                <button
                                  type="button"
                                  className="btn sm danger mt4"
                                  style={{ width: '100%', padding: '3px 6px', fontSize: 11 }}
                                  onClick={() => setCancel(item)}
                                >
                                  <X size={11} /> {item.rowType === 'waitlist' ? 'Rời chờ' : 'Hủy lịch'}
                                </button>
                              )}
                            </div>
                          ))}
                        </td>
                      );
                    })}
                    <td className="tkb-time-cell">
                      <div className="tkb-time-badge">{slotDef.timeLabel}</div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ) : (
        <>
          <div className="row between wrap mb16" style={{ gap: 12 }}>
            <Tabs
              value={tab}
              onChange={setTab}
              tabs={[
                { id: 'up', label: 'Sắp tới', count: up.length },
                { id: 'past', label: 'Lịch sử & Đánh giá', count: toReview },
              ]}
            />
            <div className="chips">
              <button className={`chip ${kindFilter === 'all' ? 'on' : ''}`} onClick={() => setKindFilter('all')}>
                Tất cả loại hình
              </button>
              <button className={`chip ${kindFilter === 'class' ? 'on' : ''}`} onClick={() => setKindFilter('class')}>
                Lớp học nhóm
              </button>
              <button className={`chip ${kindFilter === 'pt' ? 'on' : ''}`} onClick={() => setKindFilter('pt')}>
                PT 1-kèm-1
              </button>
            </div>
          </div>

          {list.length === 0 ? (
            <div className="card">
              <Empty text={tab === 'up' ? 'Không có lịch sắp tới phù hợp bộ lọc' : 'Chưa có lịch sử tập luyện'} />
            </div>
          ) : (
            <div className="col">
              {list.map((r) => (
                <div className="list-item" key={r.id}>
                  <div className="date-box">
                    <div className="d">{new Date(r.at).getDate()}</div>
                    <div className="m">Th{new Date(r.at).getMonth() + 1}</div>
                  </div>
                  <div className="grow" style={{ minWidth: 0 }}>
                    <div className="row wrap">
                      <b>{r.title}</b>
                      <Badge color={r.kind === 'pt' ? 'blue' : 'purple'} plain>
                        {r.kind === 'pt' ? 'PT 1-kèm-1' : 'Lớp nhóm'}
                      </Badge>
                      {r.source && <span className="small dim">· {r.source}</span>}
                    </div>
                    <div className="small muted">
                      {fmtWeekday(r.at)} · <b>{fmtTime(r.at)}–{fmtTime(r.end)}</b> · {r.sub}
                    </div>
                  </div>
                  <Badge color={r.status[1]}>{r.status[0]}</Badge>
                  {r.canCancel && (
                    <button className="btn sm danger" onClick={() => setCancel(r)} id={`cancel-${r.id}`}>
                      <X size={14} /> {r.rowType === 'waitlist' ? 'Rời chờ' : 'Hủy'}
                    </button>
                  )}
                  {r.canReview &&
                    (r.reviewed ? (
                      <Badge color="amber" plain>
                        <Star size={12} /> Đã đánh giá
                      </Badge>
                    ) : (
                      <button className="btn sm primary" onClick={() => setReview(r)} id={`review-${r.id}`}>
                        <MessageSquare size={14} /> Đánh giá
                      </button>
                    ))}
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {cancel && (
        <ConfirmModal
          title={cancel.rowType === 'waitlist' ? 'Rời danh sách chờ' : 'Hủy lịch'}
          danger
          confirmText="Xác nhận hủy"
          message={
            <>
              Bạn muốn hủy <b>{cancel.title}</b> lúc {fmtDateTime(cancel.at)}?
              <br />
              <br />
              {cancel.rowType === 'waitlist'
                ? '✅ Bạn sẽ rời khỏi danh sách chờ và không bị trừ lượt.'
                : cancel.raw === 'pending' || cancel.isCancelTimely
                  ? `✅ Hủy đúng hạn (trước ${cancel.cancelDeadlineHours}h) – ${cancel.kind === 'pt' ? 'buổi PT được hoàn lại vào nguồn đã trừ' : 'lượt đặt được hoàn lại'}.`
                  : `⚠️ Còn dưới ${cancel.cancelDeadlineHours} giờ – ${cancel.kind === 'pt' ? 'buổi PT' : 'lượt đặt'} sẽ bị tính là đã sử dụng.`}
            </>
          }
          onClose={() => setCancel(null)}
          onConfirm={doCancel}
        />
      )}
      {review && (
        <ReviewModal
          row={review}
          apiMode={apiMode}
          onSubmitted={loadMySchedule}
          onClose={() => setReview(null)}
        />
      )}
    </>
  );
}

function ReviewModal({
  row,
  apiMode,
  onSubmitted,
  onClose,
}: {
  row: Row;
  apiMode: boolean;
  onSubmitted: () => Promise<void>;
  onClose: () => void;
}) {
  const { user, act, toast } = useApp();
  const [rating, setRating] = useState(5);
  const [comment, setComment] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (apiMode) {
      setBusy(true);
      try {
        await bookingsApi.submitReview(row.id, { rating, comment });
        toast('Cảm ơn bạn đã đánh giá!');
        await onSubmitted();
        onClose();
      } catch (err) {
        toast((err as Error).message, 'error');
      } finally {
        setBusy(false);
      }
      return;
    }

    act((d) => L.addReview(d, user!.id, row.id, row.kind, rating, comment), 'Cảm ơn bạn đã đánh giá!');
    onClose();
  };

  return (
    <Modal
      title={`Đánh giá ${row.kind === 'pt' ? 'huấn luyện viên' : 'lớp học'}`}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>
            Để sau
          </button>
          <button className="btn primary" disabled={busy} onClick={submit} id="review-submit">
            {busy ? 'Đang gửi...' : 'Gửi đánh giá'}
          </button>
        </>
      }
    >
      <div style={{ textAlign: 'center' }}>
        <h3>{row.title}</h3>
        <div className="small muted">{fmtDateTime(row.at)}</div>
        <div className="mt16">
          <Stars value={rating} onChange={setRating} size={34} />
        </div>
        <div className="small muted mt8">{['', 'Rất tệ', 'Chưa tốt', 'Bình thường', 'Tốt', 'Tuyệt vời!'][rating]}</div>
      </div>
      <div className="field mt16">
        <label>Nhận xét</label>
        <textarea
          className="input"
          value={comment}
          onChange={(e) => setComment(e.target.value)}
          placeholder="Chia sẻ trải nghiệm của bạn..."
          id="review-comment"
        />
      </div>
    </Modal>
  );
}
