import { useCallback, useEffect, useMemo, useState } from 'react';
import { Plus, Edit2, Trash2, CalendarDays, RefreshCw, CalendarPlus, ClockAlert } from 'lucide-react';
import { useApp } from '../../../store/AppContext';
import * as L from '../../../store/logic';
import { Badge, Empty, Modal, PageHead } from '../../../components/ui';
import { fmtDateTime, fmtTime, WEEKDAYS, uid, toDateStr } from '../../../utils';
import type { GroupClass } from '../../../types';
import { classesApi, type ApiClassSessionDto, type ApiGymClassDto } from '../../../api/classesApi';
import { branchesApi } from '../../../api/branchesApi';
import { trainerService, type TrainerItem } from '../../../services/trainer.service';
import { getAuthToken, isApiNetworkError } from '../../../api/apiClient';

const DEFAULT_ROOMS = [
  'Phòng Yoga',
  'Studio A',
  'Studio B',
  'Phòng Cycling',
  'Phòng Boxing',
  'Phòng Pilates',
  'Khu chức năng',
];

type BranchOption = { id: string; name: string; rooms: string[] };

export function AdminClassesScreen() {
  const { db, act, toast } = useApp();
  const [classModal, setClassModal] = useState<ApiGymClassDto | 'new' | null>(null);
  const [rescheduleSession, setRescheduleSession] = useState<ApiClassSessionDto | null>(null);
  const [cancelSessionId, setCancelSessionId] = useState<string | null>(null);
  const [cancelReason, setCancelReason] = useState('');
  const [branchFilter, setBranchFilter] = useState<string>('all');

  const [apiMode, setApiMode] = useState<boolean>(true);
  const [loading, setLoading] = useState<boolean>(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [apiClasses, setApiClasses] = useState<ApiGymClassDto[]>([]);
  const [apiSessions, setApiSessions] = useState<ApiClassSessionDto[]>([]);
  const [apiBranches, setApiBranches] = useState<BranchOption[]>([]);
  const [apiTrainers, setApiTrainers] = useState<TrainerItem[]>([]);

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const fromIso = new Date().toISOString();
      const toIso = new Date(Date.now() + 21 * 86400000).toISOString();
      const [cls, ses, brs, trs] = await Promise.all([
        classesApi.getAll(),
        classesApi.getSessions({ from: fromIso, to: toIso }),
        branchesApi.getAll(),
        trainerService.getAllTrainers(),
      ]);

      setApiClasses(cls);
      setApiSessions(ses);
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
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  const branchOptions: BranchOption[] = useMemo(
    () =>
      apiMode
        ? apiBranches
        : db.branches.map((b) => ({ id: b.id, name: b.name, rooms: b.rooms })),
    [apiMode, apiBranches, db.branches],
  );

  const trainerOptions = useMemo(() => {
    if (apiMode) {
      return apiTrainers.map((t) => ({ id: t.id, fullName: t.fullName, branchName: t.branchName || '' }));
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
              (s) => s.classId === c.id && s.status === 'scheduled' && new Date(s.startAt).getTime() > Date.now(),
            ).length,
            active: c.active,
            averageRating: 5,
            reviewCount: 0,
          }));

    return branchFilter === 'all' ? list : list.filter((c) => c.branchId === branchFilter);
  }, [apiMode, apiClasses, db, branchFilter]);

  const upcomingSessions: ApiClassSessionDto[] = useMemo(() => {
    const now = Date.now();
    const list = apiMode
      ? apiSessions.filter((s) => s.status !== 'CANCELLED' && s.status !== 'COMPLETED' && new Date(s.startAt).getTime() > now)
      : db.sessions
          .filter((s) => s.status === 'scheduled' && new Date(s.startAt).getTime() > now)
          .map((s) => {
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
              status: 'SCHEDULED',
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

    return list
      .filter((s) => branchFilter === 'all' || s.branchId === branchFilter)
      .sort((a, b) => a.startAt.localeCompare(b.startAt));
  }, [apiMode, apiSessions, db, branchFilter]);

  const handleDeleteClass = async (cId: string) => {
    if (!confirm('Bạn có chắc muốn ngừng/xóa lớp này? Các buổi tương lai chưa diễn ra sẽ tự động bị hủy và hoàn lượt cho học viên.')) {
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
      await loadData();
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
      const added = await classesApi.generateSessions(c.id, { weeks: 2, daysOfWeek: c.daysOfWeek });
      toast(`Đã sinh thêm ${added.length} buổi học cho lớp ${c.name}.`, added.length > 0 ? 'success' : 'info');
      await loadData();
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setBusyId(null);
    }
  };

  const handleRunCutoff = async () => {
    if (!apiMode) return;
    setBusyId('cutoff');
    try {
      const res = await classesApi.processWaitlistCutoff();
      toast(res.message, res.expiredCount > 0 ? 'warning' : 'info');
      await loadData();
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
      act((d) => L.cancelSession(d, targetId, reason), 'Đã hủy buổi học và gửi thông báo hoàn lượt');
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
      await loadData();
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setBusyId(null);
    }
  };

  return (
    <>
      <PageHead
        title="Quản lý Lớp học Nhóm"
        desc="Tạo và quản lý các lớp học nhóm, phân công HLV phụ trách, kiểm tra trùng phòng/trùng lịch HLV, đổi lịch hoặc hủy buổi."
        actions={
          <div className="row wrap" style={{ gap: 8 }}>
            {apiMode && (
              <>
                <button className="btn sm" onClick={loadData} disabled={loading}>
                  <RefreshCw size={14} /> {loading ? 'Đang tải...' : 'Làm mới'}
                </button>
                <button className="btn sm" onClick={handleRunCutoff} disabled={busyId === 'cutoff'} title="Đóng danh sách chờ ở mốc T-8h">
                  <ClockAlert size={14} /> Chốt Waitlist T-8h
                </button>
              </>
            )}
            <button className="btn primary" onClick={() => setClassModal('new')} id="btn-add-class">
              <Plus size={16} /> Tạo lớp học mới
            </button>
          </div>
        }
      />

      <div className="row between wrap mt16 mb16" style={{ gap: 12 }}>
        <h2 style={{ margin: 0 }}>Danh mục lớp học ({classesList.length})</h2>
        <select className="input" style={{ maxWidth: 280 }} value={branchFilter} onChange={(e) => setBranchFilter(e.target.value)}>
          <option value="all">Tất cả chi nhánh</option>
          {branchOptions.map((b) => (
            <option key={b.id} value={b.id}>
              {b.name}
            </option>
          ))}
        </select>
      </div>

      {classesList.length === 0 ? (
        <div className="card mb24">
          <Empty text="Chưa có lớp học nhóm nào phù hợp bộ lọc." />
        </div>
      ) : (
        <div className="grid g3 mb24">
          {classesList.map((c) => (
            <div className="card hover" key={c.id} style={{ borderLeft: `4px solid ${c.color}` }}>
              <div className="card-head">
                <div>
                  <Badge color="purple" plain>
                    {c.category}
                  </Badge>
                  <h2 className="mt8">{c.name}</h2>
                </div>
                <div className="row" style={{ gap: 6 }}>
                  {apiMode && (
                    <button
                      className="btn sm"
                      disabled={busyId === c.id}
                      onClick={() => handleGenerateMore(c)}
                      title="Sinh thêm buổi học 2 tuần tới"
                    >
                      <CalendarPlus size={14} /> +2T
                    </button>
                  )}
                  <button className="btn sm ghost" onClick={() => setClassModal(c)} id={`edit-class-${c.id}`}>
                    <Edit2 size={14} /> Sửa
                  </button>
                  <button
                    className="btn sm danger"
                    disabled={busyId === c.id}
                    onClick={() => handleDeleteClass(c.id)}
                    id={`del-class-${c.id}`}
                  >
                    <Trash2 size={14} />
                  </button>
                </div>
              </div>

              <div className="divider" />

              <div className="col small" style={{ gap: 6 }}>
                <div>
                  <b>HLV phụ trách:</b> {c.trainerName || 'Chưa gán'}
                </div>
                <div>
                  <b>Địa điểm:</b> {c.room} · {c.branchName}
                </div>
                <div>
                  <b>Khung giờ:</b> {c.startTime}
                  {c.endTime ? ` – ${c.endTime}` : ''} ({c.durationMinutes} phút)
                </div>
                <div>
                  <b>Lịch lặp lại:</b> {c.daysOfWeek.map((d) => WEEKDAYS[d]).join(', ')}
                </div>
                <div>
                  <b>Sĩ số tối đa:</b> {c.capacity} học viên · <b>{c.upcomingSessionCount}</b> buổi sắp tới
                </div>
              </div>
            </div>
          ))}
        </div>
      )}

      <div className="card">
        <div className="card-head">
          <h2>
            <CalendarDays size={18} /> Các buổi học sắp diễn ra & Đổi/Hủy lịch
          </h2>
          <span className="small muted">{upcomingSessions.length} buổi học tương lai</span>
        </div>

        {upcomingSessions.length === 0 ? (
          <Empty text="Chưa có buổi học sắp diễn ra." />
        ) : (
          <div className="table-wrap">
            <table>
              <thead>
                <tr>
                  <th>Lớp học</th>
                  <th>Thời gian</th>
                  <th>Chi nhánh & Phòng</th>
                  <th>Huấn luyện viên</th>
                  <th>Đã đăng ký / Chờ</th>
                  <th style={{ textAlign: 'right' }}>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {upcomingSessions.slice(0, 25).map((s) => (
                  <tr key={s.id}>
                    <td>
                      <div className="row">
                        <span style={{ width: 8, height: 8, borderRadius: 2, background: s.color }} />
                        <b>{s.className}</b>
                      </div>
                    </td>
                    <td>{fmtDateTime(s.startAt)}</td>
                    <td className="small">
                      {s.branchName} ({s.room})
                    </td>
                    <td className="small">{s.trainerName}</td>
                    <td>
                      <Badge color={s.bookedCount >= s.capacity ? 'amber' : 'green'}>
                        {s.bookedCount}/{s.capacity}
                      </Badge>
                      {s.waitlistCount > 0 && (
                        <span className="small dim" style={{ marginLeft: 6 }}>
                          +{s.waitlistCount} chờ
                        </span>
                      )}
                    </td>
                    <td style={{ textAlign: 'right' }}>
                      <div className="row" style={{ justifyContent: 'flex-end', gap: 6 }}>
                        <button className="btn sm" onClick={() => setRescheduleSession(s)} id={`reschedule-${s.id}`}>
                          Đổi lịch
                        </button>
                        <button className="btn sm danger" onClick={() => setCancelSessionId(s.id)} id={`cancel-session-${s.id}`}>
                          Hủy buổi
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {classModal && (
        <ClassFormModal
          cls={classModal === 'new' ? null : classModal}
          apiMode={apiMode}
          branches={branchOptions}
          trainers={trainerOptions}
          onSaved={loadData}
          onClose={() => setClassModal(null)}
        />
      )}

      {rescheduleSession && (
        <RescheduleSessionModal
          session={rescheduleSession}
          apiMode={apiMode}
          branches={branchOptions}
          trainers={trainerOptions}
          onSaved={loadData}
          onClose={() => setRescheduleSession(null)}
        />
      )}

      {cancelSessionId && (
        <Modal
          title="Hủy buổi học cụ thể"
          onClose={() => setCancelSessionId(null)}
          footer={
            <>
              <button className="btn" onClick={() => setCancelSessionId(null)}>
                Quay lại
              </button>
              <button
                className="btn danger"
                disabled={busyId === cancelSessionId}
                onClick={handleConfirmCancelSession}
                id="confirm-cancel-session"
              >
                {busyId === cancelSessionId ? 'Đang xử lý...' : 'Xác nhận hủy'}
              </button>
            </>
          }
        >
          <p className="muted mb16">
            Hủy buổi học sẽ tự động hủy các lịch đặt của hội viên, hoàn trả lượt đặt và gửi thông báo đến các bên liên quan.
          </p>
          <div className="field">
            <label>Lý do hủy buổi học</label>
            <input
              className="input"
              value={cancelReason}
              onChange={(e) => setCancelReason(e.target.value)}
              placeholder="VD: Phòng tập bảo trì điện nước đột xuất"
              id="cancel-reason"
            />
          </div>
        </Modal>
      )}
    </>
  );
}

function ClassFormModal({
  cls,
  apiMode,
  branches,
  trainers,
  onSaved,
  onClose,
}: {
  cls: ApiGymClassDto | null;
  apiMode: boolean;
  branches: BranchOption[];
  trainers: { id: string; fullName: string; branchName: string }[];
  onSaved: () => Promise<void>;
  onClose: () => void;
}) {
  const { act, toast } = useApp();

  const [name, setName] = useState(cls?.name || '');
  const [room, setRoom] = useState(cls?.room || branches[0]?.rooms[0] || 'Phòng Yoga');
  const [trainerId, setTrainerId] = useState(cls?.trainerId || trainers[0]?.id || '');
  const [branchId, setBranchId] = useState(cls?.branchId || branches[0]?.id || '');
  const [daysOfWeek, setDaysOfWeek] = useState<number[]>(cls?.daysOfWeek?.length ? cls.daysOfWeek : [1, 3, 5]);
  const [startTime, setStartTime] = useState(cls?.startTime || '07:00');
  const [durationMin, setDurationMin] = useState(cls?.durationMinutes || 60);
  const [capacity, setCapacity] = useState(cls?.capacity || 15);
  const [generateWeeks, setGenerateWeeks] = useState(3);
  const [category, setCategory] = useState(cls?.category || 'Yoga');
  const [description, setDescription] = useState('');
  const [color, setColor] = useState(cls?.color || '#2ed47a');
  const [saving, setSaving] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const selectedBranch = branches.find((b) => b.id === branchId) || branches[0];
  const roomSuggestions = selectedBranch?.rooms?.length ? selectedBranch.rooms : DEFAULT_ROOMS;

  const handleSave = async () => {
    setErrorMsg(null);
    if (!name.trim()) {
      setErrorMsg('Vui lòng nhập tên lớp học.');
      return;
    }
    if (!room.trim()) {
      setErrorMsg('Vui lòng nhập hoặc chọn phòng tập.');
      return;
    }
    if (!branchId || !trainerId) {
      setErrorMsg('Vui lòng chọn chi nhánh và huấn luyện viên.');
      return;
    }
    if (daysOfWeek.length === 0) {
      setErrorMsg('Vui lòng chọn ít nhất một ngày trong tuần.');
      return;
    }

    if (!apiMode) {
      act((d) => {
        const data: GroupClass = {
          id: cls ? cls.id : uid('cl'),
          name: name.trim(),
          category: category.trim(),
          description: description.trim(),
          trainerId,
          branchId,
          room: room.trim(),
          daysOfWeek,
          startTime,
          durationMin,
          capacity,
          color,
          active: true,
        };
        L.saveClass(d, data);
      }, cls ? 'Đã cập nhật thông tin lớp học' : 'Đã tạo lớp học mới và sinh lịch');
      onClose();
      return;
    }

    setSaving(true);
    try {
      if (cls) {
        await classesApi.update(cls.id, {
          branchId,
          trainerId,
          name: name.trim(),
          room: room.trim(),
          capacity,
          dayOfWeek: daysOfWeek[0],
          daysOfWeek,
          startTime,
          durationMinutes: durationMin,
          regenerateFutureSessions: true,
          generateWeeks,
        });
        toast('Đã cập nhật lớp học và đồng bộ lịch buổi học.');
      } else {
        await classesApi.create({
          branchId,
          trainerId,
          name: name.trim(),
          room: room.trim(),
          capacity,
          dayOfWeek: daysOfWeek[0],
          daysOfWeek,
          startTime,
          durationMinutes: durationMin,
          generateWeeks,
        });
        toast('Đã tạo lớp học mới, sinh buổi học định kỳ và gửi thông báo cho HLV.');
      }
      await onSaved();
      onClose();
    } catch (err) {
      const msg = (err as Error).message;
      setErrorMsg(msg);
      toast(msg, 'error');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      wide
      title={cls ? `Chỉnh sửa lớp: ${cls.name}` : 'Tạo lớp học nhóm mới'}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>
            Hủy
          </button>
          <button className="btn primary" disabled={saving} onClick={handleSave} id="save-class">
            {saving ? 'Đang lưu...' : 'Lưu lớp học'}
          </button>
        </>
      }
    >
      {errorMsg && (
        <div className="card mb16 small" style={{ background: 'rgba(239, 68, 68, 0.1)', color: 'var(--red)' }}>
          {errorMsg}
        </div>
      )}

      <div className="form-grid">
        <div className="field">
          <label>Tên lớp học *</label>
          <input
            className="input"
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder="VD: Yoga Flow Sáng, HIIT Đốt Mỡ..."
            id="class-name"
          />
        </div>
        <div className="field">
          <label>Chi nhánh mở lớp *</label>
          <select
            className="input"
            value={branchId}
            onChange={(e) => {
              setBranchId(e.target.value);
              const nextRooms = branches.find((b) => b.id === e.target.value)?.rooms;
              if (nextRooms?.length && !room) setRoom(nextRooms[0]);
            }}
          >
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Phòng tập *</label>
          <input
            className="input"
            list="ooad-room-suggestions"
            value={room}
            onChange={(e) => setRoom(e.target.value)}
            placeholder="Chọn hoặc nhập tên phòng (VD: Studio A)"
          />
          <datalist id="ooad-room-suggestions">
            {roomSuggestions.map((r) => (
              <option key={r} value={r} />
            ))}
          </datalist>
        </div>
        <div className="field">
          <label>Huấn luyện viên phụ trách *</label>
          <select className="input" value={trainerId} onChange={(e) => setTrainerId(e.target.value)}>
            {trainers.map((t) => (
              <option key={t.id} value={t.id}>
                {t.fullName}
                {t.branchName ? ` (${t.branchName})` : ''}
              </option>
            ))}
          </select>
        </div>
        <div className="field">
          <label>Giờ bắt đầu *</label>
          <input type="time" className="input" value={startTime} onChange={(e) => setStartTime(e.target.value)} />
        </div>
        <div className="field">
          <label>Thời lượng (phút) *</label>
          <input
            type="number"
            min={15}
            step={15}
            className="input"
            value={durationMin}
            onChange={(e) => setDurationMin(+e.target.value)}
          />
        </div>
        <div className="field">
          <label>Sĩ số tối đa *</label>
          <input type="number" min={1} className="input" value={capacity} onChange={(e) => setCapacity(+e.target.value)} />
        </div>
        <div className="field">
          <label>{apiMode ? 'Sinh buổi học trước (tuần)' : 'Thể loại'}</label>
          {apiMode ? (
            <input
              type="number"
              min={1}
              max={12}
              className="input"
              value={generateWeeks}
              onChange={(e) => setGenerateWeeks(Math.max(1, +e.target.value))}
            />
          ) : (
            <input className="input" value={category} onChange={(e) => setCategory(e.target.value)} />
          )}
        </div>
        <div className="field full">
          <label>Các ngày lặp lại trong tuần *</label>
          <div className="chips">
            {[0, 1, 2, 3, 4, 5, 6].map((dayIndex) => {
              const sel = daysOfWeek.includes(dayIndex);
              return (
                <button
                  type="button"
                  key={dayIndex}
                  className={`chip ${sel ? 'on' : ''}`}
                  onClick={() => setDaysOfWeek(sel ? daysOfWeek.filter((x) => x !== dayIndex) : [...daysOfWeek, dayIndex])}
                >
                  {WEEKDAYS[dayIndex]}
                </button>
              );
            })}
          </div>
        </div>
        {!apiMode && (
          <>
            <div className="field full">
              <label>Màu hiển thị trên lịch</label>
              <input
                type="color"
                value={color}
                onChange={(e) => setColor(e.target.value)}
                style={{ width: 80, height: 38, border: 'none', background: 'transparent', cursor: 'pointer' }}
              />
            </div>
            <div className="field full">
              <label>Mô tả lớp học</label>
              <textarea
                className="input"
                value={description}
                onChange={(e) => setDescription(e.target.value)}
                placeholder="Mô tả nội dung bài tập, đối tượng phù hợp..."
              />
            </div>
          </>
        )}
      </div>
    </Modal>
  );
}

function RescheduleSessionModal({
  session,
  apiMode,
  branches,
  trainers,
  onSaved,
  onClose,
}: {
  session: ApiClassSessionDto;
  apiMode: boolean;
  branches: BranchOption[];
  trainers: { id: string; fullName: string; branchName: string }[];
  onSaved: () => Promise<void>;
  onClose: () => void;
}) {
  const { act, toast } = useApp();
  const branch = branches.find((b) => b.id === session.branchId);
  const roomSuggestions = branch?.rooms?.length ? branch.rooms : DEFAULT_ROOMS;

  const [date, setDate] = useState(toDateStr(new Date(session.startAt)));
  const [time, setTime] = useState(fmtTime(session.startAt));
  const [room, setRoom] = useState(session.room);
  const [trainerId, setTrainerId] = useState(session.trainerId);
  const [saving, setSaving] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const handleSave = async () => {
    setErrorMsg(null);
    if (!apiMode) {
      act((d) => {
        L.rescheduleSession(d, session.id, date, time, room, trainerId);
      }, 'Đã cập nhật lịch buổi học và gửi thông báo cho các bên');
      onClose();
      return;
    }

    setSaving(true);
    try {
      await classesApi.rescheduleSession(session.id, {
        sessionDate: date,
        startTime: time,
        durationMinutes: session.durationMinutes,
        room: room.trim(),
        trainerId,
      });
      toast('Đã cập nhật lịch buổi học và gửi thông báo cho học viên & HLV.');
      await onSaved();
      onClose();
    } catch (err) {
      const msg = (err as Error).message;
      setErrorMsg(msg);
      toast(msg, 'error');
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal
      title={`Đổi lịch: ${session.className}`}
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>
            Hủy
          </button>
          <button className="btn primary" disabled={saving} onClick={handleSave} id="save-reschedule">
            {saving ? 'Đang lưu...' : 'Xác nhận đổi lịch'}
          </button>
        </>
      }
    >
      {errorMsg && (
        <div className="card mb16 small" style={{ background: 'rgba(239, 68, 68, 0.1)', color: 'var(--red)' }}>
          {errorMsg}
        </div>
      )}

      <div className="form-grid">
        <div className="field">
          <label>Ngày học mới *</label>
          <input type="date" className="input" value={date} onChange={(e) => setDate(e.target.value)} />
        </div>
        <div className="field">
          <label>Giờ bắt đầu mới *</label>
          <input type="time" className="input" value={time} onChange={(e) => setTime(e.target.value)} />
        </div>
        <div className="field">
          <label>Phòng tập</label>
          <input
            className="input"
            list="ooad-reschedule-rooms"
            value={room}
            onChange={(e) => setRoom(e.target.value)}
          />
          <datalist id="ooad-reschedule-rooms">
            {roomSuggestions.map((r) => (
              <option key={r} value={r} />
            ))}
          </datalist>
        </div>
        <div className="field">
          <label>Huấn luyện viên phụ trách</label>
          <select className="input" value={trainerId} onChange={(e) => setTrainerId(e.target.value)}>
            {trainers.map((t) => (
              <option key={t.id} value={t.id}>
                {t.fullName}
              </option>
            ))}
          </select>
        </div>
      </div>
      <p className="small muted mt16">
        💡 Hệ thống kiểm tra xung đột phòng & lịch HLV, đồng thời tự động gửi thông báo đến tất cả học viên đã đặt.
      </p>
    </Modal>
  );
}

export default AdminClassesScreen;
