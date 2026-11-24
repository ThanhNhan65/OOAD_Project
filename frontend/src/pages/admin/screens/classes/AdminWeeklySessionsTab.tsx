import { useMemo, useState } from 'react';
import { ArrowLeft, ArrowRight, CalendarDays, X } from 'lucide-react';
import type { ApiClassSessionDto, ApiGymClassDto } from '../../../../api/classesApi';
import { Badge, Empty } from '../../../../components/ui';
import { WEEKDAYS, addDays, fmtDate, fmtTime, toDateStr, todayStr } from '../../../../utils';
import { getMondayStr, shortDayMonth } from '../../../member/MemberBooking';

interface AdminWeeklySessionsTabProps {
  weekIdx: number;
  weekDays: string[];
  onWeekIdxChange: (next: number) => void;
  sessions: ApiClassSessionDto[];
  classes: ApiGymClassDto[];
  classFilter: string;
  onClassFilterChange: (id: string) => void;
  loading: boolean;
  nowMs: number;
  onReschedule: (session: ApiClassSessionDto) => void;
  onCancelSession: (sessionId: string) => void;
}

type StatusFilter = 'active' | 'upcoming' | 'all' | 'cancelled';

function formatWeekLabel(offset: number): string {
  const mon = getMondayStr(todayStr(), offset);
  const sun = addDays(mon, 6);
  const range = `${shortDayMonth(mon)} – ${fmtDate(sun)}`;
  if (offset === 0) return `Tuần này (${range})`;
  if (offset === -1) return `Tuần trước (${range})`;
  if (offset === 1) return `Tuần sau (${range})`;
  return `Tuần ${offset > 0 ? `+${offset}` : offset} (${range})`;
}

export function AdminWeeklySessionsTab({
  weekIdx,
  weekDays,
  onWeekIdxChange,
  sessions,
  classes,
  classFilter,
  onClassFilterChange,
  loading,
  nowMs,
  onReschedule,
  onCancelSession,
}: AdminWeeklySessionsTabProps) {
  const [selectedCategory, setSelectedCategory] = useState<string>('all');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('active');

  const weekOffsets = useMemo(() => {
    const base = [-2, -1, 0, 1, 2, 3, 4, 5, 6];
    if (!base.includes(weekIdx)) {
      base.push(weekIdx);
      base.sort((a, b) => a - b);
    }
    return base;
  }, [weekIdx]);

  const statusFilteredSessions = useMemo(() => {
    const filtered = sessions
      .filter((s) => {
        const localDate = toDateStr(new Date(s.startAt));
        if (!weekDays.includes(localDate)) return false;
        if (classFilter !== 'all' && s.classId !== classFilter) return false;

        if (statusFilter === 'active') return s.status !== 'CANCELLED';
        if (statusFilter === 'upcoming') {
          return (
            s.status !== 'CANCELLED' &&
            s.status !== 'COMPLETED' &&
            new Date(s.startAt).getTime() > nowMs
          );
        }
        if (statusFilter === 'cancelled') return s.status === 'CANCELLED';
        return true;
      })
      .sort((a, b) => a.startAt.localeCompare(b.startAt));

    return Array.from(new Map(filtered.map((s) => [s.id, s])).values());
  }, [sessions, weekDays, classFilter, statusFilter, nowMs]);

  const categoryCounts = useMemo(() => {
    const map = new Map<string, { category: string; color: string; count: number }>();
    for (const s of statusFilteredSessions) {
      const cat = s.category || 'Khác';
      const existing = map.get(cat);
      if (existing) {
        existing.count += 1;
      } else {
        map.set(cat, { category: cat, color: s.color || '#7c5cff', count: 1 });
      }
    }
    return Array.from(map.values()).sort((a, b) => a.category.localeCompare(b.category, 'vi'));
  }, [statusFilteredSessions]);

  const effectiveCategoryFilter = useMemo(
    () =>
      selectedCategory !== 'all' && categoryCounts.some((c) => c.category === selectedCategory)
        ? selectedCategory
        : 'all',
    [selectedCategory, categoryCounts],
  );

  const filteredSessions = useMemo(() => {
    if (effectiveCategoryFilter === 'all') return statusFilteredSessions;
    return statusFilteredSessions.filter((s) => (s.category || 'Khác') === effectiveCategoryFilter);
  }, [statusFilteredSessions, effectiveCategoryFilter]);

  const activeCategoryColor =
    effectiveCategoryFilter === 'all'
      ? 'var(--primary)'
      : categoryCounts.find((c) => c.category === effectiveCategoryFilter)?.color ?? 'var(--primary)';

  const canModifySession = (s: ApiClassSessionDto) =>
    s.status !== 'CANCELLED' &&
    s.status !== 'COMPLETED' &&
    new Date(s.startAt).getTime() > nowMs;

  return (
    <>
      <div className="card mb12" style={{ padding: '10px 14px' }}>
        <div className="row between wrap mb8" style={{ gap: 8 }}>
          <div className="row wrap" style={{ gap: 5 }}>
            <CalendarDays size={15} color="var(--primary)" style={{ flexShrink: 0 }} />
            <button
              type="button"
              className="btn sm"
              style={{ fontSize: 12, padding: '5px 8px' }}
              onClick={() => onWeekIdxChange(weekIdx - 1)}
              title="Tuần trước"
              id="admin-prev-week"
            >
              <ArrowLeft size={13} /> Trước
            </button>

            <select
              className="input sm"
              style={{ width: 'auto', minWidth: 195, fontSize: 12.5, padding: '5px 9px' }}
              value={weekIdx}
              onChange={(e) => onWeekIdxChange(Number(e.target.value))}
              id="admin-week-select"
            >
              {weekOffsets.map((offset) => (
                <option key={offset} value={offset}>
                  {formatWeekLabel(offset)}
                </option>
              ))}
            </select>

            <button
              type="button"
              className="btn sm"
              style={{ fontSize: 12, padding: '5px 9px' }}
              disabled={weekIdx === 0}
              onClick={() => onWeekIdxChange(0)}
              id="admin-this-week"
            >
              Tuần này
            </button>

            <button
              type="button"
              className="btn sm"
              style={{ fontSize: 12, padding: '5px 8px' }}
              onClick={() => onWeekIdxChange(weekIdx + 1)}
              title="Tuần sau"
              id="admin-next-week"
            >
              Sau <ArrowRight size={13} />
            </button>

            {loading && (
              <span className="small muted" style={{ fontSize: 11.5, marginLeft: 4 }}>
                Đang tải...
              </span>
            )}
          </div>

          <div className="row wrap" style={{ gap: 6 }}>
            <select
              className="input sm"
              style={{ width: 'auto', maxWidth: 200, fontSize: 12.5, padding: '5px 9px' }}
              value={classFilter}
              onChange={(e) => onClassFilterChange(e.target.value)}
              id="admin-class-filter"
            >
              <option value="all">Tất cả lớp ({classes.length})</option>
              {classes.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name} ({c.startTime})
                </option>
              ))}
            </select>

            {classFilter !== 'all' && (
              <button
                type="button"
                className="btn sm ghost"
                style={{ fontSize: 12, padding: '5px 8px' }}
                onClick={() => onClassFilterChange('all')}
                id="admin-clear-class-filter"
              >
                <X size={12} /> Bỏ lọc
              </button>
            )}

            <select
              className="input sm"
              style={{ width: 'auto', fontSize: 12.5, padding: '5px 9px' }}
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value as StatusFilter)}
              id="admin-status-filter"
            >
              <option value="active">Đang hoạt động</option>
              <option value="upcoming">Sắp diễn ra</option>
              <option value="all">Tất cả</option>
              <option value="cancelled">Đã hủy</option>
            </select>
          </div>
        </div>

        <div className="row wrap" style={{ gap: 6, paddingTop: 8, borderTop: '1px solid var(--border)' }}>
          <span className="small muted bold" style={{ fontSize: 12 }}>Bộ môn:</span>
          <div className="chips">
            <button
              type="button"
              className={`chip ${effectiveCategoryFilter === 'all' ? 'on' : ''}`}
              style={{ fontSize: 11.5, padding: '4px 10px' }}
              onClick={() => setSelectedCategory('all')}
              id="admin-cat-all"
            >
              Tất cả ({statusFilteredSessions.length})
            </button>
            {categoryCounts.map((c) => (
              <button
                type="button"
                key={c.category}
                className={`chip ${effectiveCategoryFilter === c.category ? 'on' : ''}`}
                style={{ fontSize: 11.5, padding: '4px 10px' }}
                onClick={() => setSelectedCategory(c.category)}
                id={`admin-cat-${c.category}`}
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
          borderLeft:
            effectiveCategoryFilter === 'all' ? undefined : `4px solid ${activeCategoryColor}`,
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
              {effectiveCategoryFilter === 'all'
                ? 'Danh sách buổi học — Tất cả bộ môn'
                : `Bộ môn: ${effectiveCategoryFilter}`}
            </h2>
          </div>
          <span className="small muted" style={{ fontSize: 12 }}>
            <b>{filteredSessions.length}</b> buổi trong tuần
          </span>
        </div>

        {filteredSessions.length === 0 ? (
          <div style={{ padding: 24 }}>
            <Empty text="Không có buổi học nào trong tuần này phù hợp bộ lọc." />
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
              <tbody key={`${effectiveCategoryFilter}-${classFilter}-${statusFilter}-${weekIdx}`}>
                {filteredSessions.map((s) => {
                  const isCancelled = s.status === 'CANCELLED';
                  const isCompleted = s.status === 'COMPLETED';
                  const full = s.bookedCount >= s.capacity;
                  const editable = canModifySession(s);
                  const startDateObj = new Date(s.startAt);
                  const dateLabel = `${WEEKDAYS[startDateObj.getDay()]}, ${shortDayMonth(toDateStr(startDateObj))}`;

                  return (
                    <tr key={s.id} style={{ opacity: isCancelled ? 0.65 : 1 }}>
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
                          {fmtTime(s.startAt)} – {fmtTime(s.endAt)}
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
                        ) : isCompleted ? (
                          <Badge color="blue">Hoàn thành</Badge>
                        ) : new Date(s.startAt).getTime() <= nowMs ? (
                          <Badge color="gray">Đã qua</Badge>
                        ) : full ? (
                          <Badge color="amber">Kín chỗ</Badge>
                        ) : (
                          <Badge color="green">Mở ĐK</Badge>
                        )}
                      </td>
                      <td style={{ textAlign: 'right', whiteSpace: 'nowrap' }}>
                        {editable ? (
                          <div className="row" style={{ justifyContent: 'flex-end', gap: 5 }}>
                            <button
                              className="btn sm xs"
                              onClick={() => onReschedule(s)}
                              id={`reschedule-${s.id}`}
                            >
                              Đổi lịch
                            </button>
                            <button
                              className="btn sm danger xs"
                              onClick={() => onCancelSession(s.id)}
                              id={`cancel-session-${s.id}`}
                            >
                              Hủy buổi
                            </button>
                          </div>
                        ) : (
                          <span className="dim">—</span>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  );
}
