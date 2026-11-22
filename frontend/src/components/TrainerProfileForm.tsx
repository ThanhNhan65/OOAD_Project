import React, { useEffect, useState } from 'react';
import type { TrainerItem, UpdateTrainerProfilePayload } from '../services/trainer.service';
import { branchesApi, type BranchApiDto } from '../api/branchesApi';

interface TrainerProfileFormProps {
  trainer: TrainerItem;
  onSave: (payload: UpdateTrainerProfilePayload) => Promise<void>;
  loading?: boolean;
}

export const TrainerProfileForm: React.FC<TrainerProfileFormProps> = ({ trainer, onSave, loading = false }) => {
  const [branches, setBranches] = useState<BranchApiDto[]>([]);
  const [specialty, setSpecialty] = useState(trainer.specialty || '');
  const [experienceYears, setExperienceYears] = useState(trainer.experienceYears || 0);
  const [bio, setBio] = useState(trainer.bio || '');
  const [phone, setPhone] = useState(trainer.phone || '');
  const [branchId, setBranchId] = useState(trainer.branchId || '');

  useEffect(() => {
    branchesApi
      .getAll()
      .then((list) => {
        setBranches(list);
        setBranchId((prev) => prev || trainer.branchId || list[0]?.id || '');
      })
      .catch(() => {});
  }, [trainer.branchId]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await onSave({
      specialty,
      experienceYears: Number(experienceYears),
      bio,
      phone,
      branchId,
    });
  };

  return (
    <form className="card col" style={{ padding: 24, gap: 16 }} onSubmit={handleSubmit}>
      <h2 style={{ fontSize: 18 }}>Cập nhật hồ sơ Huấn luyện viên</h2>

      <div className="grid g2" style={{ gap: 16 }}>
        <div className="field">
          <label>Họ và tên</label>
          <input className="input" value={trainer.fullName} disabled />
        </div>
        <div className="field">
          <label>Email</label>
          <input className="input" value={trainer.email} disabled />
        </div>
      </div>

      <div className="grid g2" style={{ gap: 16 }}>
        <div className="field">
          <label>Số điện thoại liên hệ *</label>
          <input
            className="input"
            value={phone}
            onChange={(e) => setPhone(e.target.value)}
            placeholder="VD: 0912345678"
            required
          />
        </div>
        <div className="field">
          <label>Chi nhánh trực thuộc *</label>
          <select
            className="input"
            value={branchId}
            onChange={(e) => setBranchId(e.target.value)}
            required
          >
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="grid g2" style={{ gap: 16 }}>
        <div className="field">
          <label>Chuyên môn chính (cách nhau dấu phẩy) *</label>
          <input
            className="input"
            value={specialty}
            onChange={(e) => setSpecialty(e.target.value)}
            placeholder="VD: Tăng cơ, Giảm mỡ, Boxing, Giãn cơ"
            required
          />
        </div>
        <div className="field">
          <label>Số năm kinh nghiệm (năm) *</label>
          <input
            type="number"
            min={0}
            max={50}
            className="input"
            value={experienceYears}
            onChange={(e) => setExperienceYears(Number(e.target.value))}
            required
          />
        </div>
      </div>

      <div className="field">
        <label>Tiểu sử / Giới thiệu bản thân & Chứng chỉ</label>
        <textarea
          className="input"
          rows={4}
          value={bio}
          onChange={(e) => setBio(e.target.value)}
          placeholder="Giới thiệu về phương pháp huấn luyện, chứng chỉ quốc tế NASM/ACE, thế mạnh của bạn..."
        />
      </div>

      <div className="row" style={{ justifyContent: 'flex-end', marginTop: 8 }}>
        <button type="submit" className="btn primary" disabled={loading}>
          {loading ? 'Đang lưu...' : 'Lưu thay đổi hồ sơ'}
        </button>
      </div>
    </form>
  );
};
