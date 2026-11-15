import React, { useEffect, useState } from 'react';
import { Calendar, AlertCircle } from 'lucide-react';
import { trainerService, type TrainerAvailabilityItem, type TrainerScheduleItem } from '../../services/trainer.service';
import { TimeSlotPicker } from '../../components/TimeSlotPicker';
import { AvailabilityGrid } from '../../components/AvailabilityGrid';
import { PageHead, Badge } from '../../components/ui';
import { useApp } from '../../store/AppContext';

export const TrainerDashboardSchedulePage: React.FC = () => {
  const { user, toast } = useApp();
  const [activeTab, setActiveTab] = useState<'availability' | 'schedule'>('availability');
  const [availabilities, setAvailabilities] = useState<TrainerAvailabilityItem[]>([]);
  const [schedule, setSchedule] = useState<TrainerScheduleItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    loadData();
  }, [user]);

  const loadData = async () => {
    try {
      setLoading(true);
      setErrorMessage(null);

      // Lấy profile hiện tại
      const me = await trainerService.getMyProfile();
      if (me) {
        const slots = await trainerService.getAvailability(me.id);
        setAvailabilities(slots);
      }

      // Lấy toàn bộ lịch dạy (lớp + PT)
      const mySchedule = await trainerService.getMySchedule();
      setSchedule(mySchedule);
    } catch {
      // Fallback cho môi trường demo / mock
      const tomorrow = new Date();
      tomorrow.setDate(tomorrow.getDate() + 1);
      const dayStr = tomorrow.toISOString().slice(0, 10);

      setAvailabilities([
        { id: 'av-demo-1', trainerId: user?.id || 'tr1', startAt: `${dayStr}T07:00:00Z`, endAt: `${dayStr}T08:00:00Z`, isBooked: false },
        { id: 'av-demo-2', trainerId: user?.id || 'tr1', startAt: `${dayStr}T09:00:00Z`, endAt: `${dayStr}T10:00:00Z`, isBooked: true },
        { id: 'av-demo-3', trainerId: user?.id || 'tr1', startAt: `${dayStr}T16:00:00Z`, endAt: `${dayStr}T17:00:00Z`, isBooked: false },
      ]);

      setSchedule([
        {
          id: 'sc-1',
          type: 'CLASS',
          title: 'Lớp Yoga Buổi Sáng',
          startAt: `${dayStr}T06:00:00Z`,
          endAt: `${dayStr}T07:00:00Z`,
          room: 'Studio A',
          status: 'SCHEDULED',
        },
        {
          id: 'sc-2',
          type: 'PT',
          title: 'PT 1-kèm-1',
          startAt: `${dayStr}T09:00:00Z`,
          endAt: `${dayStr}T10:00:00Z`,
          clientName: 'Nguyễn Văn A',
          clientPhone: '0901234567',
          status: 'Confirmed',
        },
      ]);
    } finally {
      setLoading(false);
    }
  };

  const handleAddSlot = async (startAt: string, endAt: string) => {
    try {
      setActionLoading(true);
      setErrorMessage(null);

      // Chặn trùng giờ dạy lớp nhóm cục bộ nếu mock
      const conflictClass = schedule.find(
        (s) => s.type === 'CLASS' && s.startAt < endAt && s.endAt > startAt
      );

      if (conflictClass) {
        throw new Error(`Khung giờ này trùng với lịch dạy lớp nhóm: "${conflictClass.title}". Không thể mở ca rảnh.`);
      }

      const newSlot = await trainerService.addAvailability({ startAt, endAt });
      setAvailabilities((prev) => [...prev, newSlot].sort((a, b) => a.startAt.localeCompare(b.startAt)));
      toast('Đã thêm khung giờ rảnh thành công!');
    } catch (err: any) {
      const msg = err.message || 'Không thể tạo khung giờ rảnh.';
      setErrorMessage(msg);
      toast(msg, 'error');
    } finally {
      setActionLoading(false);
    }
  };

  const handleDeleteSlot = async (slotId: string) => {
    try {
      setActionLoading(true);
      await trainerService.deleteAvailability(slotId);
      setAvailabilities((prev) => prev.filter((s) => s.id !== slotId));
      toast('Đã xóa khung giờ rảnh.');
    } catch (err: any) {
      toast(err.message || 'Lỗi khi xóa khung giờ.', 'error');
    } finally {
      setActionLoading(false);
    }
  };

  return (
    <>
      <PageHead
        title="Quản lý Lịch biểu & Khung giờ rảnh (Schedule)"
        desc="Mở các khung giờ rảnh nhận học viên PT và theo dõi lịch tổng hợp (Lớp nhóm + Lịch PT 1-kèm-1)."
      />

      {errorMessage && (
        <div className="card row mb16" style={{ background: 'rgba(255, 77, 109, 0.1)', borderColor: 'var(--red)', padding: 12, gap: 10 }}>
          <AlertCircle size={18} color="var(--red)" />
          <span style={{ color: 'var(--red)', fontWeight: 600 }}>{errorMessage}</span>
        </div>
      )}

      {/* TABS CHUYỂN ĐỔI */}
      <div className="tabs mb20">
        <button
          className={`tab ${activeTab === 'availability' ? 'on' : ''}`}
          onClick={() => setActiveTab('availability')}
        >
          ⏰ Khung giờ rảnh (Availability)
        </button>
        <button
          className={`tab ${activeTab === 'schedule' ? 'on' : ''}`}
          onClick={() => setActiveTab('schedule')}
        >
          📋 Toàn bộ lịch làm việc (Lớp nhóm & PT)
        </button>
      </div>

      {activeTab === 'availability' ? (
        <div className="col" style={{ gap: 20 }}>
          <TimeSlotPicker onAddSlot={handleAddSlot} loading={actionLoading} />

          <div className="card" style={{ padding: 20 }}>
            <div className="card-head between mb16">
              <div>
                <h2>Danh sách khung giờ rảnh đã mở</h2>
                <p className="small muted">Hội viên chỉ có thể đặt lịch PT vào những khung giờ bạn đã tick sẵn sàng.</p>
              </div>
            </div>

            {loading ? (
              <p className="muted">Đang tải...</p>
            ) : (
              <AvailabilityGrid
                availabilities={availabilities}
                onDeleteSlot={handleDeleteSlot}
                isOwner={true}
              />
            )}
          </div>
        </div>
      ) : (
        <div className="card" style={{ padding: 20 }}>
          <h2 className="mb16"><Calendar size={18} /> Lịch trình giảng dạy tổng hợp</h2>

          {schedule.length === 0 ? (
            <p className="muted">Chưa có lịch dạy nào trong tuần này.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>Loại lịch</th>
                    <th>Nội dung</th>
                    <th>Thời gian</th>
                    <th>Chi tiết / Học viên</th>
                    <th>Trạng thái</th>
                  </tr>
                </thead>
                <tbody>
                  {schedule.map((item) => (
                    <tr key={item.id}>
                      <td>
                        {item.type === 'CLASS' ? (
                          <Badge color="purple">Lớp nhóm</Badge>
                        ) : (
                          <Badge color="blue">PT 1-kèm-1</Badge>
                        )}
                      </td>
                      <td><b>{item.title}</b></td>
                      <td>
                        {new Date(item.startAt).toLocaleDateString('vi-VN')} ·{' '}
                        {new Date(item.startAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })} -{' '}
                        {new Date(item.endAt).toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit' })}
                      </td>
                      <td>
                        {item.type === 'CLASS' ? (
                          <span>Phòng: <b>{item.room || 'Studio'}</b></span>
                        ) : (
                          <span>Học viên: <b>{item.clientName}</b> {item.clientPhone ? `(${item.clientPhone})` : ''}</span>
                        )}
                      </td>
                      <td>
                        <Badge color={item.status === 'Confirmed' || item.status === 'SCHEDULED' ? 'green' : 'amber'}>
                          {item.status || 'Active'}
                        </Badge>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </>
  );
};
