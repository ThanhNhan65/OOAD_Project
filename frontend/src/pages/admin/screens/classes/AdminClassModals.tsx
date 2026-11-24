import { useMemo, useState } from 'react';
import { classesApi, type ApiClassSessionDto, type ApiGymClassDto } from '../../../../api/classesApi';
import { Modal } from '../../../../components/ui';
import { useApp } from '../../../../store/AppContext';
import * as L from '../../../../store/logic';
import type { GroupClass } from '../../../../types';
import { WEEKDAYS, atTime, fmtTime, toDateStr, uid } from '../../../../utils';
import { DEFAULT_ROOMS, type BranchOption, type TrainerOption } from './types';

function rangesOverlap(startAMs: number, endAMs: number, startBMs: number, endBMs: number): boolean {
  return startAMs < endBMs && startBMs < endAMs;
}

export function ClassFormModal({
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
  trainers: TrainerOption[];
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
      const ok = act((d) => {
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
        return L.saveClass(d, data);
      }, cls ? 'Đã cập nhật thông tin lớp học' : 'Đã tạo lớp học mới và sinh lịch');
      if (ok) {
        onClose();
      } else {
        setErrorMsg('Không thể lưu lớp học do trùng phòng, trùng lịch HLV hoặc dữ liệu không hợp lệ.');
      }
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

export function RescheduleSessionModal({
  session,
  apiMode,
  branches,
  trainers,
  allSessions,
  onSaved,
  onClose,
}: {
  session: ApiClassSessionDto;
  apiMode: boolean;
  branches: BranchOption[];
  trainers: TrainerOption[];
  allSessions: ApiClassSessionDto[];
  onSaved: () => Promise<void>;
  onClose: () => void;
}) {
  const { db, act, toast } = useApp();
  const branch = branches.find((b) => b.id === session.branchId);
  const roomSuggestions = branch?.rooms?.length ? branch.rooms : DEFAULT_ROOMS;

  const [date, setDate] = useState(toDateStr(new Date(session.startAt)));
  const [time, setTime] = useState(fmtTime(session.startAt));
  const [room, setRoom] = useState(session.room);
  const [trainerId, setTrainerId] = useState(session.trainerId);
  const [nowMs, setNowMs] = useState<number>(() => Date.now());
  const [saving, setSaving] = useState(false);
  const [errorMsg, setErrorMsg] = useState<string | null>(null);

  const liveConflicts = useMemo(() => {
    const conflicts: string[] = [];
    const cleanRoom = room.trim();

    if (!date || !time) {
      conflicts.push('Vui lòng chọn ngày học và giờ bắt đầu.');
      return conflicts;
    }
    if (!cleanRoom) {
      conflicts.push('Vui lòng nhập hoặc chọn phòng tập.');
    }
    if (!trainerId) {
      conflicts.push('Vui lòng chọn huấn luyện viên phụ trách.');
    }

    const newStartIso = atTime(date, time);
    const newStartMs = new Date(newStartIso).getTime();
    if (Number.isNaN(newStartMs)) {
      conflicts.push('Ngày hoặc giờ học không hợp lệ.');
      return conflicts;
    }
    if (newStartMs <= nowMs) {
      conflicts.push('Thời gian học mới phải nằm ở tương lai.');
    }

    const newEndMs = newStartMs + session.durationMinutes * 60000;
    const normalizedRoom = cleanRoom.toLowerCase();

    for (const other of allSessions) {
      if (other.id === session.id || other.status === 'CANCELLED') continue;
      const otherStartMs = new Date(other.startAt).getTime();
      const otherEndMs = new Date(other.endAt).getTime();
      if (!rangesOverlap(newStartMs, newEndMs, otherStartMs, otherEndMs)) continue;

      if (other.classId === session.classId) {
        conflicts.push(`Lớp '${session.className}' đã có buổi học khác trùng khung giờ này.`);
      }

      if (
        normalizedRoom &&
        other.branchId === session.branchId &&
        (other.room || '').trim().toLowerCase() === normalizedRoom
      ) {
        conflicts.push(
          `Trùng phòng '${cleanRoom}': lớp '${other.className}' đang dùng phòng này (${fmtTime(other.startAt)}–${fmtTime(other.endAt)}).`,
        );
      }

      if (trainerId && other.trainerId === trainerId) {
        const trainerName = trainers.find((t) => t.id === trainerId)?.fullName ?? other.trainerName;
        conflicts.push(
          `Trùng HLV: ${trainerName} đang dạy lớp '${other.className}' tại ${other.branchName} (${fmtTime(other.startAt)}–${fmtTime(other.endAt)}).`,
        );
      }
    }

    if (!apiMode && trainerId) {
      const ptConflict = db.ptBookings.find(
        (b) =>
          b.trainerId === trainerId &&
          ['pending', 'confirmed'].includes(b.status) &&
          rangesOverlap(
            newStartMs,
            newEndMs,
            new Date(b.startAt).getTime(),
            new Date(b.endAt).getTime(),
          ),
      );
      if (ptConflict) {
        conflicts.push(
          `Trùng lịch PT 1-kèm-1 của HLV (${fmtTime(ptConflict.startAt)}–${fmtTime(ptConflict.endAt)}).`,
        );
      }
    }

    return conflicts;
  }, [date, time, room, trainerId, nowMs, allSessions, session, trainers, apiMode, db.ptBookings]);

  const handleSave = async () => {
    setErrorMsg(null);
    if (liveConflicts.length > 0) {
      setErrorMsg(liveConflicts[0]);
      return;
    }

    const cleanRoom = room.trim();

    if (!apiMode) {
      const ok = act(
        (d) => L.rescheduleSession(d, session.id, date, time, cleanRoom, trainerId),
        'Đã cập nhật lịch buổi học và gửi thông báo cho các bên',
      );
      if (ok) {
        onClose();
      } else {
        setErrorMsg('Không thể đổi lịch do vi phạm quy định trùng phòng hoặc trùng lịch HLV.');
      }
      return;
    }

    setSaving(true);
    try {
      await classesApi.rescheduleSession(session.id, {
        sessionDate: date,
        startTime: time,
        durationMinutes: session.durationMinutes,
        room: cleanRoom,
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
          <button
            className="btn primary"
            disabled={saving || liveConflicts.length > 0}
            onClick={handleSave}
            id="save-reschedule"
          >
            {saving ? 'Đang lưu...' : 'Xác nhận đổi lịch'}
          </button>
        </>
      }
    >
      {liveConflicts.length > 0 && (
        <div
          className="card mb16 small"
          style={{ background: 'rgba(239, 68, 68, 0.1)', color: 'var(--red)' }}
          id="reschedule-conflicts"
        >
          <div className="bold mb4">Không thể đổi lịch vì vi phạm quy định:</div>
          <ul style={{ margin: 0, paddingLeft: 18 }}>
            {liveConflicts.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        </div>
      )}

      {errorMsg && !liveConflicts.includes(errorMsg) && (
        <div className="card mb16 small" style={{ background: 'rgba(239, 68, 68, 0.1)', color: 'var(--red)' }}>
          {errorMsg}
        </div>
      )}

      <div className="form-grid">
        <div className="field">
          <label>Ngày học mới *</label>
          <input
            type="date"
            className="input"
            value={date}
            onChange={(e) => {
              setDate(e.target.value);
              setNowMs(Date.now());
            }}
          />
        </div>
        <div className="field">
          <label>Giờ bắt đầu mới *</label>
          <input
            type="time"
            className="input"
            value={time}
            onChange={(e) => {
              setTime(e.target.value);
              setNowMs(Date.now());
            }}
          />
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
        💡 Quy định đổi lịch: Cùng một bộ môn có thể mở nhiều lớp song song, nhưng tại cùng khung giờ mỗi phòng (trong cùng chi nhánh) và mỗi HLV chỉ được phụ trách tối đa 1 lớp/ca dạy.
      </p>
    </Modal>
  );
}

export function CancelSessionModal({
  reason,
  busy,
  onReasonChange,
  onConfirm,
  onClose,
}: {
  reason: string;
  busy: boolean;
  onReasonChange: (value: string) => void;
  onConfirm: () => void;
  onClose: () => void;
}) {
  return (
    <Modal
      title="Hủy buổi học cụ thể"
      onClose={onClose}
      footer={
        <>
          <button className="btn" onClick={onClose}>
            Quay lại
          </button>
          <button
            className="btn danger"
            disabled={busy}
            onClick={onConfirm}
            id="confirm-cancel-session"
          >
            {busy ? 'Đang xử lý...' : 'Xác nhận hủy'}
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
          value={reason}
          onChange={(e) => onReasonChange(e.target.value)}
          placeholder="VD: Phòng tập bảo trì điện nước đột xuất"
          id="cancel-reason"
        />
      </div>
    </Modal>
  );
}
