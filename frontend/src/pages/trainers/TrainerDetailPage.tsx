import React, { useEffect, useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { ArrowLeft, Star, MapPin, Award, Calendar, CheckCircle2 } from 'lucide-react';
import { trainerService, type TrainerItem, type TrainerAvailabilityItem } from '../../services/trainer.service';
import { AvailabilityGrid } from '../../components/AvailabilityGrid';
import { Badge, Modal } from '../../components/ui';
import { useApp } from '../../store/AppContext';

export const TrainerDetailPage: React.FC = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { db, user, toast } = useApp();

  const [trainer, setTrainer] = useState<TrainerItem | null>(null);
  const [availabilities, setAvailabilities] = useState<TrainerAvailabilityItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [selectedSlot, setSelectedSlot] = useState<TrainerAvailabilityItem | null>(null);
  const [note, setNote] = useState('');
  const [bookingLoading, setBookingLoading] = useState(false);

  useEffect(() => {
    if (id) {
      loadTrainerData(id);
    }
  }, [id]);

  const loadTrainerData = async (trainerId: string) => {
    try {
      setLoading(true);
      const data = await trainerService.getTrainerById(trainerId);
      setTrainer(data);

      const slots = await trainerService.getAvailability(trainerId);
      setAvailabilities(slots);
    } catch {
      // Fallback từ mock DB
      const u = db.users.find((x: any) => x.id === trainerId && x.role === 'trainer')
        || db.users.find((x: any) => x.role === 'trainer');
      if (u) {
        const b = db.branches.find((x: any) => x.id === u.branchId);
        setTrainer({
          id: u.id,
          userId: u.id,
          branchId: u.branchId || db.branches[0]?.id || '',
          fullName: u.fullName || 'Huấn luyện viên',
          email: u.email || '',
          phone: u.phone,
          branchName: b?.name,
          specialty: u.trainer?.specialties?.join(', ') || 'Thể hình, PT',
          experienceYears: u.trainer?.experienceYears || 3,
          bio: u.trainer?.bio || 'Huấn luyện viên tận tâm, nhiều năm kinh nghiệm.',
          averageRating: 5.0,
          reviewCount: 15,
        });

        // Mock slots
        const tomorrow = new Date();
        tomorrow.setDate(tomorrow.getDate() + 1);
        const dayStr = tomorrow.toISOString().slice(0, 10);
        setAvailabilities([
          { id: 'av-1', trainerId, startAt: `${dayStr}T08:00:00Z`, endAt: `${dayStr}T09:00:00Z`, isBooked: false },
          { id: 'av-2', trainerId, startAt: `${dayStr}T15:00:00Z`, endAt: `${dayStr}T16:00:00Z`, isBooked: false },
          { id: 'av-3', trainerId, startAt: `${dayStr}T17:00:00Z`, endAt: `${dayStr}T18:00:00Z`, isBooked: true },
        ]);
      }
    } finally {
      setLoading(false);
    }
  };

  const handleConfirmBook = async () => {
    if (!selectedSlot || !trainer) return;

    if (!user) {
      toast('Vui lòng đăng nhập để đặt lịch tập.', 'warning');
      navigate('/login');
      return;
    }

    try {
      setBookingLoading(true);
      // Giả lập đặt lịch thành công
      toast(`Đặt lịch PT với ${trainer.fullName} thành công! Lịch tập đang chờ HLV xác nhận.`);
      setSelectedSlot(null);
      // Đánh dấu slot đã đặt
      setAvailabilities((prev) =>
        prev.map((s) => (s.id === selectedSlot.id ? { ...s, isBooked: true } : s))
      );
    } catch (err: any) {
      toast(err.message || 'Lỗi khi đặt lịch', 'error');
    } finally {
      setBookingLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="card text-center" style={{ padding: 48 }}>
        <p className="muted">Đang tải thông tin huấn luyện viên...</p>
      </div>
    );
  }

  if (!trainer) {
    return (
      <div className="card text-center" style={{ padding: 48 }}>
        <h3>Không tìm thấy huấn luyện viên</h3>
        <button className="btn mt16" onClick={() => navigate('/trainers')}>
          <ArrowLeft size={16} /> Quay lại danh sách
        </button>
      </div>
    );
  }

  return (
    <>
      <button className="btn ghost sm mb16" onClick={() => navigate('/trainers')} style={{ gap: 6 }}>
        <ArrowLeft size={16} /> Quay lại danh sách HLV
      </button>

      {/* HEADER HỒ SƠ HLV */}
      <div className="card mb24" style={{ padding: 24 }}>
        <div className="row wrap between" style={{ alignItems: 'flex-start', gap: 20 }}>
          <div className="row" style={{ gap: 20 }}>
            <div
              style={{
                width: 80,
                height: 80,
                borderRadius: '50%',
                background: 'linear-gradient(135deg, var(--primary), #a78bfa)',
                color: '#fff',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 32,
                fontWeight: 800,
              }}
            >
              {trainer.fullName.slice(0, 1).toUpperCase()}
            </div>

            <div>
              <div className="row" style={{ gap: 10 }}>
                <h1 style={{ fontSize: 24 }}>{trainer.fullName}</h1>
                <div className="row" style={{ gap: 4, background: 'rgba(255,176,32,0.12)', padding: '4px 10px', borderRadius: 999 }}>
                  <Star size={16} color="#f59e0b" fill="#f59e0b" />
                  <span className="bold" style={{ color: '#d97706' }}>{trainer.averageRating.toFixed(1)}</span>
                  <span className="small dim">({trainer.reviewCount} đánh giá)</span>
                </div>
              </div>

              <div className="row wrap muted mt8" style={{ gap: 16 }}>
                <div className="row" style={{ gap: 4 }}>
                  <MapPin size={15} /> <span>{trainer.branchName || 'Chi nhánh OOAD'}</span>
                </div>
                <div className="row" style={{ gap: 4 }}>
                  <Award size={15} color="var(--primary)" /> <span>{trainer.experienceYears} năm kinh nghiệm</span>
                </div>
              </div>

              {trainer.specialty && (
                <div className="row wrap mt12" style={{ gap: 6 }}>
                  {trainer.specialty.split(',').map((s, idx) => (
                    <Badge key={idx} color="purple">
                      {s.trim()}
                    </Badge>
                  ))}
                </div>
              )}
            </div>
          </div>
        </div>

        {trainer.bio && (
          <div className="mt20 pt16" style={{ borderTop: '1px solid var(--border)' }}>
            <h3 style={{ fontSize: 15 }} className="mb6">Giới thiệu & Chuyên môn:</h3>
            <p className="muted" style={{ lineHeight: 1.6 }}>{trainer.bio}</p>
          </div>
        )}
      </div>

      {/* LỊCH RẢNH ĐẶT LỚP */}
      <div className="card mb24" style={{ padding: 20 }}>
        <div className="card-head between mb16">
          <div>
            <h2><Calendar size={18} /> Khung giờ nhận học viên PT 1-kèm-1</h2>
            <p className="small muted">Chọn khung giờ rảnh phù hợp để đặt lịch tập luyện với huấn luyện viên</p>
          </div>
        </div>

        <AvailabilityGrid
          availabilities={availabilities}
          onBookSlot={(slot) => setSelectedSlot(slot)}
          isOwner={false}
        />
      </div>

      {/* MODAL XÁC NHẬN ĐẶT LỊCH */}
      {selectedSlot && (
        <Modal
          title="Xác nhận đặt lịch PT 1-kèm-1"
          onClose={() => setSelectedSlot(null)}
          footer={
            <div className="row" style={{ gap: 8, justifyContent: 'flex-end' }}>
              <button className="btn" onClick={() => setSelectedSlot(null)}>Hủy</button>
              <button className="btn primary" onClick={handleConfirmBook} disabled={bookingLoading}>
                {bookingLoading ? 'Đang gửi...' : 'Xác nhận đặt lịch'}
              </button>
            </div>
          }
        >
          <div className="col" style={{ gap: 14 }}>
            <div className="card" style={{ padding: 14, background: 'var(--surface-2)' }}>
              <div><b>Huấn luyện viên:</b> {trainer.fullName}</div>
              <div className="mt4"><b>Chi nhánh:</b> {trainer.branchName}</div>
              <div className="mt4">
                <b>Thời gian tập:</b> {new Date(selectedSlot.startAt).toLocaleString('vi-VN')}
              </div>
            </div>

            <div className="field">
              <label>Ghi chú mục tiêu tập luyện (tùy chọn)</label>
              <textarea
                className="input"
                rows={3}
                placeholder="VD: Muốn tập trung giảm mỡ bụng, siết cơ vai, tiền sử đau lưng..."
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
            </div>

            <div className="small muted row" style={{ gap: 6 }}>
              <CheckCircle2 size={16} color="var(--green)" />
              Buổi tập sẽ được tính vào số buổi PT khả dụng trong gói tập của bạn.
            </div>
          </div>
        </Modal>
      )}
    </>
  );
};
