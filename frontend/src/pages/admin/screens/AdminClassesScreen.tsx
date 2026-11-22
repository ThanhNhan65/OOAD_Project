import { useCallback, useEffect, useMemo, useState } from 'react';
import { Plus, RefreshCw } from 'lucide-react';
import { getAuthToken, isApiNetworkError } from '../../../api/apiClient';
import { branchesApi } from '../../../api/branchesApi';
import { classesApi, type ApiClassSessionDto, type ApiGymClassDto } from '../../../api/classesApi';
import { PageHead, Tabs } from '../../../components/ui';
import { trainerService, type TrainerItem } from '../../../services/trainer.service';
import { useApp } from '../../../store/AppContext';
import * as L from '../../../store/logic';
import { addDays, atTime, todayStr } from '../../../utils';
import { getMondayStr } from '../../member/MemberBooking';
import { AdminClassCatalogTab } from './classes/AdminClassCatalogTab';
import {
  CancelSessionModal,
  ClassFormModal,
  RescheduleSessionModal,
} from './classes/AdminClassModals';
import { AdminWeeklySessionsTab } from './classes/AdminWeeklySessionsTab';
import { DEFAULT_ROOMS, type BranchOption, type TrainerOption } from './classes/types';

export function AdminClassesScreen() {
  const { db, act, toast } = useApp();

  const [activeTab, setActiveTab] = useState<'catalog' | 'sessions'>('catalog');
  const [branchFilter, setBranchFilter] = useState<string>('all');
  const [classFilter, setClassFilter] = useState<string>('all');
  const [catalogSearch, setCatalogSearch] = useState<string>('');
  const [weekIdx, setWeekIdx] = useState<number>(0);
  const [nowMs, setNowMs] = useState<number>(() => Date.now());

  const [classModal, setClassModal] = useState<ApiGymClassDto | 'new' | null>(null);
  const [rescheduleSession, setRescheduleSession] = useState<ApiClassSessionDto | null>(null);
  const [cancelSessionId, setCancelSessionId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');

  const [apiMode, setApiMode] = useState<boolean>(true);
  const [loadingMeta, setLoadingMeta] = useState<boolean>(false);
  const [loadingSessions, setLoadingSessions] = useState<boolean>(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [apiClasses, setApiClasses] = useState<ApiGymClassDto[]>([]);
  const [apiSessions, setApiSessions] = useState<ApiClassSessionDto[]>([]);
  const [apiBranches, setApiBranches] = useState<BranchOption[]>([]);
  const [apiTrainers, setApiTrainers] = useState<TrainerItem[]>([]);

  const weekMonday = useMemo(() => getMondayStr(todayStr(), weekIdx), [weekIdx]);
  const weekDays = useMemo(
    () => Array.from({ length: 7 }, (_, i) => addDays(weekMonday, i)),
    [weekMonday],
  );

  const loadCatalogAndMeta = useCallback(async () => {
    setLoadingMeta(true);
    try {
      const [cls, brs, trs] = await Promise.all([
        classesApi.getAll(),
        branchesApi.getAll(),
        trainerService.getAllTrainers(),
      ]);
      setApiClasses(cls);
      setApiBranches(
        brs.map((b) => ({
          id: b.id,
          name: b.name,
          rooms: (b as unknown as { rooms?: string[] }).rooms ?? DEFAULT_ROOMS,
        })),
      );
      setApiTrainers(trs);
      setApiMode(true);
    } catch (err) {
      if (isApiNetworkError(err)) {
        setApiMode(false);
      } else {
        toast((err as Error).message, 'error');
      }
    } finally {
      setLoadingMeta(false);
    }
  }, [toast]);

  const loadWeekSessions = useCallback(async () => {
    setLoadingSessions(true);
    try {
      const fromIso = atTime(weekDays[0], '00:00');
      const toIso = atTime(weekDays[6], '23:59');
      const ses = await classesApi.getSessions({ from: fromIso, to: toIso });
      setApiSessions(ses);
      setNowMs(Date.now());
      setApiMode(true);
    } catch (err) {
      if (isApiNetworkError(err)) {
        setApiMode(false);
      } else {
        toast((err as Error).message, 'error');
      }
    } finally {
      setLoadingSessions(false);
    }
  }, [weekDays, toast]);

  const loadAll = useCallback(async () => {
    await Promise.all([loadCatalogAndMeta(), loadWeekSessions()]);
  }, [loadCatalogAndMeta, loadWeekSessions]);

  useEffect(() => {
    let cancelled = false;
    Promise.all([
      classesApi.getAll(),
      branchesApi.getAll(),
      trainerService.getAllTrainers(),
    ])
      .then(([cls, brs, trs]) => {
        if (cancelled) return;
        setApiClasses(cls);
        setApiBranches(
          brs.map((b) => ({
            id: b.id,
            name: b.name,
            rooms: (b as unknown as { rooms?: string[] }).rooms ?? DEFAULT_ROOMS,
          })),
        );
        setApiTrainers(trs);
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
  }, [toast]);

  useEffect(() => {
    let cancelled = false;
    const fromIso = atTime(weekDays[0], '00:00');
    const toIso = atTime(weekDays[6], '23:59');
    classesApi
      .getSessions({ from: fromIso, to: toIso })
      .then((ses) => {
        if (cancelled) return;
        setApiSessions(ses);
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
  }, [weekDays, toast]);

  const branchOptions: BranchOption[] = useMemo(
    () =>
      apiMode
        ? apiBranches
        : db.branches.map((b) => ({ id: b.id, name: b.name, rooms: b.rooms })),
    [apiMode, apiBranches, db.branches],
  );

  const trainerOptions: TrainerOption[] = useMemo(() => {
    if (apiMode) {
      return apiTrainers.map((t) => ({
        id: t.id,
        fullName: t.fullName,
        branchName: t.branchName || '',
      }));
    }
    return db.users
      .filter((u) => u.role === 'trainer' && !u.locked)
      .map((u) => ({ id: u.id, fullName: u.fullName, branchName: '' }));
  }, [apiMode, apiTrainers, db.users]);

  const classesList: ApiGymClassDto[] = useMemo(() => {
    const list = apiMode
      ? apiClasses.filter((c) => c.active)
      : db.classes
          .filter((c) => c.active)
          .map((c) => ({
            id: c.id,
            branchId: c.branchId,
            branchName: L.branchById(db, c.branchId)?.name ?? '',
            trainerId: c.trainerId,
            trainerUserId: c.trainerId,
            trainerName: L.userById(db, c.trainerId)?.fullName ?? 'Chưa gán',
            name: c.name,
            room: c.room,
            capacity: c.capacity,
            dayOfWeek: c.daysOfWeek[0] ?? 1,
            daysOfWeek: c.daysOfWeek,
            startTime: c.startTime,
            endTime: '',
            durationMinutes: c.durationMin,
            category: c.category,
            color: c.color,
            upcomingSessionCount: db.sessions.filter(
              (s) =>
                s.classId === c.id &&
                s.status === 'scheduled' &&
                new Date(s.startAt).getTime() > nowMs,
            ).length,
            active: c.active,
            averageRating: 5,
            reviewCount: 0,
          }));

    return branchFilter === 'all' ? list : list.filter((c) => c.branchId === branchFilter);
  }, [apiMode, apiClasses, db, branchFilter, nowMs]);

  const effectiveClassFilter = useMemo(
    () => (classFilter !== 'all' && classesList.some((c) => c.id === classFilter) ? classFilter : 'all'),
    [classesList, classFilter],
  );

  const allComparisonSessions: ApiClassSessionDto[] = useMemo(() => {
    if (apiMode) return apiSessions;
    return db.sessions.map((s) => {
      const c = L.classById(db, s.classId)!;
      const tr = L.userById(db, s.trainerId);
      const booked = L.bookedCount(db, s.id);
      const wl = L.waitlistOf(db, s.id).length;
      return {
        id: s.id,
        classId: s.classId,
        className: c?.name ?? '',
        category: c?.category ?? '',
        color: c?.color ?? '#7c5cff',
        branchId: c?.branchId ?? '',
        branchName: L.branchById(db, c?.branchId)?.name ?? '',
        trainerId: s.trainerId,
        trainerUserId: s.trainerId,
        trainerName: tr?.fullName ?? '',
        room: s.room,
        capacity: c?.capacity ?? 20,
        sessionDate: s.date,
        startAt: s.startAt,
        endAt: s.endAt,
        durationMinutes: c?.durationMin ?? 60,
        status:
          s.status === 'cancelled'
            ? 'CANCELLED'
            : s.status === 'completed'
              ? 'COMPLETED'
              : booked >= (c?.capacity ?? 20)
                ? 'FULL'
                : 'SCHEDULED',
        bookedCount: booked,
        availableSlots: Math.max(0, (c?.capacity ?? 20) - booked),
        waitlistCount: wl,
        attendanceConfirmed: s.attendanceConfirmed,
        attendedCount: 0,
        canJoinWaitlist: false,
        hoursUntilStart: 24,
        averageRating: 5,
        reviewCount: 0,
      };
    });
  }, [apiMode, apiSessions, db]);

  const weekSessionsList: ApiClassSessionDto[] = useMemo(() => {
    return allComparisonSessions
      .filter((s) => weekDays.includes(s.sessionDate.slice(0, 10)) || apiMode)
      .filter((s) => branchFilter === 'all' || s.branchId === branchFilter)
      .sort((a, b) => a.startAt.localeCompare(b.startAt));
  }, [allComparisonSessions, weekDays, apiMode, branchFilter]);

  const activeWeekSessionCount = useMemo(
    () => weekSessionsList.filter((s) => s.status !== 'CANCELLED').length,
    [weekSessionsList],
  );

  const handleDeleteClass = async (cId: string) => {
    if (
      !confirm(
        'Bạn có chắc muốn ngừng/xóa lớp này? Các buổi tương lai chưa diễn ra sẽ tự động bị hủy và hoàn lượt cho học viên.',
      )
    ) {
      return;
    }

    if (!apiMode) {
      act((d) => L.deleteClass(d, cId), 'Đã ngừng hoạt động lớp học');
      return;
    }

    if (!getAuthToken()) {
      toast('Vui lòng đăng nhập lại bằng tài khoản Admin.', 'error');
      return;
    }

    setBusyId(cId);
    try {
      await classesApi.remove(cId);
      toast('Đã hủy các buổi sắp tới và xóa lớp học thành công.');
      await loadAll();
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setBusyId(null);
    }
  };

  const handleGenerateMore = async (c: ApiGymClassDto) => {
    if (!apiMode) return;
    setBusyId(c.id);
    try {
      const added = await classesApi.generateSessions(c.id, {
        weeks: 2,
        daysOfWeek: c.daysOfWeek,
      });
      toast(
        `Đã sinh thêm ${added.length} buổi học cho lớp ${c.name}.`,
        added.length > 0 ? 'success' : 'info',
      );
      await loadAll();
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setBusyId(null);
    }
  };

  const handleConfirmCancelSession = async () => {
    if (!cancelSessionId) return;
    const targetId = cancelSessionId;
    const reason = cancelReason.trim() || 'HLV bận đột xuất / Sự cố phòng tập';

    if (!apiMode) {
      act(
        (d) => L.cancelSession(d, targetId, reason),
        'Đã hủy buổi học và gửi thông báo hoàn lượt',
      );
      setCancelSessionId(null);
      setCancelReason('');
      return;
    }

    setBusyId(targetId);
    try {
      await classesApi.cancelSession(targetId, reason);
      toast('Đã hủy buổi học, hoàn lượt đặt và gửi thông báo cho học viên & HLV.');
      setCancelSessionId(null);
      setCancelReason('');
      await loadAll();
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setBusyId(null);
    }
  };

  const handleViewClassSchedule = (classId: string) => {
    setClassFilter(classId);
    setActiveTab('sessions');
  };

  const loading = loadingMeta || loadingSessions;

  return (
    <>
      <PageHead
        title="Quản lý Lớp học Nhóm"
        desc="Quản lý danh mục lớp định kỳ và xem lịch buổi học từng tuần để đổi lịch hoặc hủy buổi nhanh chóng."
        actions={
          <div className="row wrap" style={{ gap: 8 }}>
            {apiMode && (
              <button className="btn sm" onClick={loadAll} disabled={loading}>
                <RefreshCw size={14} /> {loading ? 'Đang tải...' : 'Làm mới'}
              </button>
            )}
            <button className="btn primary" onClick={() => setClassModal('new')} id="btn-add-class">
              <Plus size={16} /> Tạo lớp học mới
            </button>
          </div>
        }
      />

      <div className="row between wrap mb16" style={{ gap: 12, alignItems: 'center' }}>
        <Tabs
          value={activeTab}
          onChange={setActiveTab}
          tabs={[
            { id: 'catalog', label: 'Danh mục lớp học', count: classesList.length },
            { id: 'sessions', label: 'Lịch buổi học theo tuần', count: activeWeekSessionCount },
          ]}
        />

        <select
          className="input"
          style={{ maxWidth: 280 }}
          value={branchFilter}
          onChange={(e) => {
            setBranchFilter(e.target.value);
            setClassFilter('all');
          }}
          id="admin-branch-filter"
        >
          <option value="all">Tất cả chi nhánh</option>
          {branchOptions.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </div>

      {activeTab === 'catalog' ? (
        <AdminClassCatalogTab
          classes={classesList}
          apiMode={apiMode}
          busyId={busyId}
          search={catalogSearch}
          onSearchChange={setCatalogSearch}
          onEdit={(cls) => setClassModal(cls)}
          onDelete={handleDeleteClass}
          onGenerateMore={handleGenerateMore}
          onViewClassSchedule={handleViewClassSchedule}
          onCreateNew={() => setClassModal('new')}
        />
      ) : (
        <AdminWeeklySessionsTab
          weekIdx={weekIdx}
          weekDays={weekDays}
          onWeekIdxChange={(next) => {
            setWeekIdx(next);
            setNowMs(Date.now());
          }}
          sessions={weekSessionsList}
          classes={classesList}
          classFilter={effectiveClassFilter}
          onClassFilterChange={setClassFilter}
          loading={loadingSessions}
          nowMs={nowMs}
          onReschedule={(session) => setRescheduleSession(session)}
          onCancelSession={(id) => setCancelSessionId(id)}
        />
      )}

      {classModal && (
        <ClassFormModal
          cls={classModal === 'new' ? null : classModal}
          apiMode={apiMode}
          branches={branchOptions}
          trainers={trainerOptions}
          onSaved={loadAll}
          onClose={() => setClassModal(null)}
        />
      )}

      {rescheduleSession && (
        <RescheduleSessionModal
          session={rescheduleSession}
          apiMode={apiMode}
          branches={branchOptions}
          trainers={trainerOptions}
          allSessions={allComparisonSessions}
          onSaved={loadAll}
          onClose={() => setRescheduleSession(null)}
        />
      )}

      {cancelSessionId && (
        <CancelSessionModal
          reason={cancelReason}
          busy={busyId === cancelSessionId}
          onReasonChange={setCancelReason}
          onConfirm={handleConfirmCancelSession}
          onClose={() => setCancelSessionId(null)}
        />
      )}
    </>
  );
}

export default AdminClassesScreen;
