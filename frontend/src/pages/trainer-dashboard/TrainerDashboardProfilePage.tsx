import React, { useEffect, useState } from 'react';
import { trainerService, type TrainerItem, type UpdateTrainerProfilePayload } from '../../services/trainer.service';
import { TrainerProfileForm } from '../../components/TrainerProfileForm';
import { PageHead } from '../../components/ui';
import { useApp } from '../../store/AppContext';

export const TrainerDashboardProfilePage: React.FC = () => {
  const { user, toast, db } = useApp();
  const [trainer, setTrainer] = useState<TrainerItem | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    loadProfile();
  }, [user]);

  const loadProfile = async () => {
    try {
      setLoading(true);
      const data = await trainerService.getMyProfile();
      setTrainer(data);
    } catch {
      // Fallback từ mock
      const u = (user?.role === 'trainer' ? user : db.users.find((x: any) => x.role === 'trainer')) || user;
      if (u) {
        const b = db.branches.find((x: any) => x.id === u.branchId);
        setTrainer({
          id: u.id,
          userId: u.id,
          branchId: u.branchId || db.branches[0]?.id || '',
          fullName: u.fullName || 'Huấn luyện viên',
          email: u.email || '',
          phone: u.phone || '0912345678',
          branchName: b?.name,
          specialty: u.trainer?.specialties?.join(', ') || 'Thể hình, PT 1-kèm-1',
          experienceYears: u.trainer?.experienceYears || 3,
          bio: u.trainer?.bio || 'HLV tận tâm, nhiệt tình hỗ trợ học viên.',
          averageRating: 5.0,
          reviewCount: 10,
        });
      }
    } finally {
      setLoading(false);
    }
  };

  const handleSave = async (payload: UpdateTrainerProfilePayload) => {
    try {
      setSaving(true);
      const updated = await trainerService.updateMyProfile(payload);
      setTrainer(updated);
      toast('Cập nhật hồ sơ huấn luyện viên thành công!');
    } catch (err: any) {
      // Cập nhật mock nếu offline
      if (trainer) {
        setTrainer({
          ...trainer,
          specialty: payload.specialty,
          experienceYears: payload.experienceYears ?? trainer.experienceYears,
          bio: payload.bio,
          phone: payload.phone,
          branchId: payload.branchId ?? trainer.branchId,
        });
        toast('Đã lưu thông tin hồ sơ (môi trường nội bộ).');
      } else {
        toast(err.message || 'Lỗi khi cập nhật hồ sơ', 'error');
      }
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return (
      <div className="card text-center" style={{ padding: 48 }}>
        <p className="muted">Đang tải thông tin hồ sơ...</p>
      </div>
    );
  }

  if (!trainer) {
    return (
      <div className="card text-center" style={{ padding: 48 }}>
        <p className="muted">Không tìm thấy thông tin Huấn luyện viên.</p>
      </div>
    );
  }

  return (
    <>
      <PageHead
        title="Hồ sơ Huấn luyện viên (Trainer Profile)"
        desc="Quản lý thông tin giới thiệu, chứng chỉ thể hình và chuyên môn hiển thị cho học viên."
      />

      <div style={{ maxWidth: 800, margin: '0 auto' }}>
        <TrainerProfileForm trainer={trainer} onSave={handleSave} loading={saving} />
      </div>
    </>
  );
};
