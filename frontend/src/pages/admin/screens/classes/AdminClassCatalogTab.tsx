import { useMemo } from 'react';
import { CalendarDays, CalendarPlus, Edit2, Plus, Search, Trash2 } from 'lucide-react';
import type { ApiGymClassDto } from '../../../../api/classesApi';
import { Badge, Empty } from '../../../../components/ui';
import { WEEKDAYS } from '../../../../utils';

interface AdminClassCatalogTabProps {
  classes: ApiGymClassDto[];
  apiMode: boolean;
  busyId: string | null;
  search: string;
  onSearchChange: (v: string) => void;
  onEdit: (cls: ApiGymClassDto) => void;
  onDelete: (id: string) => void;
  onGenerateMore: (cls: ApiGymClassDto) => void;
  onViewClassSchedule: (classId: string) => void;
  onCreateNew: () => void;
}

export function AdminClassCatalogTab({
  classes,
  apiMode,
  busyId,
  search,
  onSearchChange,
  onEdit,
  onDelete,
  onGenerateMore,
  onViewClassSchedule,
  onCreateNew,
}: AdminClassCatalogTabProps) {
  const filteredClasses = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return classes;
    return classes.filter(
      (c) =>
        c.name.toLowerCase().includes(q) ||
        (c.trainerName || '').toLowerCase().includes(q) ||
        (c.room || '').toLowerCase().includes(q) ||
        (c.category || '').toLowerCase().includes(q) ||
        (c.branchName || '').toLowerCase().includes(q),
    );
  }, [classes, search]);

  return (
    <>
      <div className="card mb12" style={{ padding: '10px 14px' }}>
        <div className="row between wrap" style={{ gap: 10 }}>
          <div style={{ position: 'relative', minWidth: 240, flex: '1 1 300px', maxWidth: 420 }}>
            <Search
              size={14}
              className="dim"
              style={{ position: 'absolute', left: 10, top: '50%', transform: 'translateY(-50%)' }}
            />
            <input
              className="input sm"
              style={{ paddingLeft: 32, width: '100%', fontSize: 12.5 }}
              placeholder="Tìm theo tên lớp, HLV, phòng, bộ môn..."
              value={search}
              onChange={(e) => onSearchChange(e.target.value)}
              id="search-admin-classes"
            />
          </div>

          <div className="row small muted" style={{ gap: 10, fontSize: 12.5 }}>
            <span>
              Hiển thị <b>{filteredClasses.length}</b> / {classes.length} lớp học định kỳ
            </span>
          </div>
        </div>
      </div>

      {filteredClasses.length === 0 ? (
        <div className="card" style={{ padding: 32, textAlign: 'center' }}>
          <Empty
            text={
              search.trim()
                ? 'Không tìm thấy lớp học nào phù hợp với từ khóa tìm kiếm.'
                : 'Chưa có lớp học nhóm nào trong chi nhánh này.'
            }
          />
          <div className="row mt16" style={{ justifyContent: 'center', gap: 10 }}>
            {search.trim() && (
              <button className="btn sm" onClick={() => onSearchChange('')}>
                Xóa từ khóa
              </button>
            )}
            <button className="btn sm primary" onClick={onCreateNew}>
              <Plus size={14} /> Tạo lớp học mới
            </button>
          </div>
        </div>
      ) : (
        <div className="grid g3">
          {filteredClasses.map((c) => (
            <div
              className="card hover"
              key={c.id}
              style={{ borderLeft: `4px solid ${c.color}`, padding: 14 }}
            >
              <div className="row between wrap" style={{ gap: 8, alignItems: 'flex-start' }}>
                <div style={{ minWidth: 0, flex: '1 1 auto' }}>
                  <Badge color="purple" plain>
                    {c.category}
                  </Badge>
                  <h2 className="mt8" style={{ fontSize: 15, marginBottom: 0 }}>
                    {c.name}
                  </h2>
                </div>
                <div className="row" style={{ gap: 4, flexShrink: 0 }}>
                  <button
                    className="btn sm ghost"
                    style={{ fontSize: 11.5, padding: '4px 8px' }}
                    onClick={() => onEdit(c)}
                    id={`edit-class-${c.id}`}
                  >
                    <Edit2 size={13} /> Sửa
                  </button>
                  <button
                    className="btn sm danger"
                    style={{ fontSize: 11.5, padding: '4px 8px' }}
                    disabled={busyId === c.id}
                    onClick={() => onDelete(c.id)}
                    id={`del-class-${c.id}`}
                    title="Ngừng / hủy lớp học"
                  >
                    <Trash2 size={13} /> Hủy lớp
                  </button>
                </div>
              </div>

              <div className="divider" style={{ margin: '10px 0' }} />

              <div className="col" style={{ gap: 4, fontSize: 12.5 }}>
                <div>
                  <b>HLV:</b> {c.trainerName || 'Chưa gán'}
                </div>
                <div>
                  <b>Địa điểm:</b> {c.room} · {c.branchName}
                </div>
                <div>
                  <b>Khung giờ:</b> {c.startTime}
                  {c.endTime ? ` – ${c.endTime}` : ''} ({c.durationMinutes}p) ·{' '}
                  <b>Lặp:</b> {c.daysOfWeek.map((d) => WEEKDAYS[d]).join(', ')}
                </div>
                <div>
                  <b>Sĩ số tối đa:</b> {c.capacity} học viên · <b>{c.upcomingSessionCount}</b> buổi sắp tới
                </div>
              </div>

              <div
                className="row between mt12"
                style={{ paddingTop: 10, borderTop: '1px solid var(--border)', gap: 6 }}
              >
                {apiMode ? (
                  <button
                    className="btn sm"
                    style={{ fontSize: 11.5, padding: '4px 8px' }}
                    disabled={busyId === c.id}
                    onClick={() => onGenerateMore(c)}
                    title="Sinh thêm buổi học 2 tuần tới"
                  >
                    <CalendarPlus size={13} /> Sinh +2 tuần
                  </button>
                ) : (
                  <span />
                )}
                <button
                  className="btn sm"
                  style={{ fontSize: 11.5, padding: '4px 9px' }}
                  onClick={() => onViewClassSchedule(c.id)}
                  id={`view-schedule-${c.id}`}
                >
                  <CalendarDays size={13} /> Xem lịch tuần
                </button>
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  );
}
