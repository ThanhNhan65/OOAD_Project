import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Dumbbell, Search, Filter } from 'lucide-react';
import { trainerService, type TrainerItem } from '../../services/trainer.service';
import { TrainerCard } from '../../components/TrainerCard';
import { useApp } from '../../store/AppContext';
import { PageHead } from '../../components/ui';
import type { User, Branch } from '../../types';

export const TrainersListPage: React.FC = () => {
  const navigate = useNavigate();
  const { db } = useApp();
  const [trainers, setTrainers] = useState<TrainerItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [branchFilter, setBranchFilter] = useState('');

  useEffect(() => {
    loadTrainers();
  }, [branchFilter]);

  const loadTrainers = async () => {
    try {
      setLoading(true);
      const data = await trainerService.getAllTrainers(branchFilter || undefined);
      setTrainers(data);
    } catch {
      // Fallback từ mock DB nếu backend offline
      const mockList: TrainerItem[] = db.users
        .filter((u: User) => u.role === 'trainer')
        .map((u: User) => {
          const b = db.branches.find((x: Branch) => x.id === u.branchId);
          return {
            id: u.id,
            userId: u.id,
            branchId: u.branchId || db.branches[0]?.id || '',
            fullName: u.fullName || 'HLV',
            email: u.email || '',
            phone: u.phone,
            branchName: b?.name,
            specialty: u.trainer?.specialties?.join(', ') || 'Thể hình, PT 1-kèm-1',
            experienceYears: u.trainer?.experienceYears || 3,
            bio: u.trainer?.bio || 'Huấn luyện viên chuyên nghiệp tại OOAD',
            averageRating: 5.0,
            reviewCount: 12,
          };
        });
      setTrainers(mockList);
    } finally {
      setLoading(false);
    }
  };

  const filtered = trainers.filter((t) => {
    const q = search.toLowerCase();
    return (
      t.fullName.toLowerCase().includes(q) ||
      (t.specialty && t.specialty.toLowerCase().includes(q)) ||
      (t.branchName && t.branchName.toLowerCase().includes(q))
    );
  });

  return (
    <>
      <PageHead
        title="Đội ngũ Huấn luyện viên (Personal Trainers)"
        desc="Lựa chọn Huấn luyện viên chuyên môn phù hợp để cùng bạn đạt mục tiêu thể hình nhanh nhất."
      />

      <div className="card row between wrap mb24" style={{ padding: 14, gap: 12 }}>
        <div className="row" style={{ gap: 8, flex: 1, minWidth: 260 }}>
          <Search size={18} className="muted" />
          <input
            className="input sm"
            style={{ border: 'none', background: 'transparent' }}
            placeholder="Tìm theo tên HLV, chuyên môn (vd: Boxing, Giảm mỡ)..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
          />
        </div>

        <div className="row" style={{ gap: 8 }}>
          <Filter size={16} className="muted" />
          <select
            className="input sm"
            value={branchFilter}
            onChange={(e) => setBranchFilter(e.target.value)}
          >
            <option value="">Tất cả chi nhánh</option>
            {db.branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </select>
        </div>
      </div>

      {loading ? (
        <div className="card text-center" style={{ padding: 48 }}>
          <p className="muted">Đang tải danh sách huấn luyện viên...</p>
        </div>
      ) : filtered.length === 0 ? (
        <div className="card text-center" style={{ padding: 48 }}>
          <Dumbbell size={36} color="var(--primary)" style={{ margin: '0 auto 12px' }} />
          <h3>Không tìm thấy huấn luyện viên phù hợp</h3>
          <p className="muted mt4">Hãy thử tìm với từ khóa hoặc chi nhánh khác.</p>
        </div>
      ) : (
        <div className="grid g3" style={{ gap: 18 }}>
          {filtered.map((trainer) => (
            <TrainerCard
              key={trainer.id}
              trainer={trainer}
              onSelect={(t) => navigate(`/trainers/${t.id}`)}
            />
          ))}
        </div>
      )}
    </>
  );
};
