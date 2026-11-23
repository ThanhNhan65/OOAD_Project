import { useEffect, useState, useCallback } from 'react';
import { RefreshCw } from 'lucide-react';
import { useApp } from '../../../store/AppContext';
import * as L from '../../../store/logic';
import { Avatar, Empty, PageHead, Stars } from '../../../components/ui';
import { fmtDate } from '../../../utils';
import { reviewsApi, type ApiTrainerReviewSummaryDto } from '../../../api/reviewsApi';
import { useTrainerStats } from './useTrainerStats';
import { isApiNetworkError } from '../../../api/apiClient';

export function TrainerReviewsScreen() {
  const { db } = useApp();
  const s = useTrainerStats();
  const [loading, setLoading] = useState<boolean>(false);
  const [apiSummary, setApiSummary] = useState<ApiTrainerReviewSummaryDto | null>(null);

  const fetchReviews = useCallback(async () => {
    setLoading(true);
    try {
      const summary = await reviewsApi.getMyReviews();
      setApiSummary(summary);
    } catch (err) {
      if (!isApiNetworkError(err)) {
        console.warn('Lỗi tải đánh giá từ API, dùng dữ liệu local:', err);
      }
      setApiSummary(null);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchReviews();
  }, [fetchReviews]);

  const avg = apiSummary ? apiSummary.averageRating : s.avg;
  const total = apiSummary ? apiSummary.totalReviews : s.reviews.length;
  const dist = [5, 4, 3, 2, 1].map((n) => {
    const c = apiSummary ? (apiSummary.ratingDistribution[n] || 0) : s.reviews.filter((r) => r.rating === n).length;
    return { n, c };
  });

  return (
    <>
      <PageHead
        title="Đánh giá của tôi"
        desc="Phản hồi từ hội viên sau các buổi PT và lớp nhóm."
        actions={
          <button className="btn ghost sm" onClick={fetchReviews} disabled={loading}>
            <RefreshCw size={14} className={loading ? 'spin' : ''} /> Làm mới
          </button>
        }
      />
      <div className="grid g3 mb16">
        <div className="card" style={{ textAlign: 'center' }}>
          <div style={{ fontSize: 52, fontWeight: 800 }} className="grad-text">
            {avg ? avg.toFixed(2) : '—'}
          </div>
          <Stars value={avg} size={20} />
          <div className="small muted mt8">{total} đánh giá</div>
        </div>
        <div className="card span2">
          {dist.map((d) => (
            <div className="row mb16" key={d.n} style={{ marginBottom: 8 }}>
              <span className="small" style={{ width: 40 }}>{d.n} ★</span>
              <div className="progress grow">
                <div
                  style={{
                    width: `${total ? (d.c / total) * 100 : 0}%`,
                    background: 'var(--grad-amber)',
                  }}
                />
              </div>
              <span className="small dim" style={{ width: 30, textAlign: 'right' }}>
                {d.c}
              </span>
            </div>
          ))}
        </div>
      </div>

      {apiSummary ? (
        apiSummary.reviews.length === 0 ? (
          <div className="card"><Empty text="Chưa có đánh giá" /></div>
        ) : (
          <div className="grid g2">
            {apiSummary.reviews.map((r) => (
              <div className="card" key={r.id}>
                <div className="row">
                  <Avatar user={{ fullName: r.memberName, avatarColor: '#00d1b2' }} size="sm" />
                  <div className="grow">
                    <b>{r.memberName}</b>
                    <div className="small dim">
                      {fmtDate(r.createdAt)} · {r.targetType === 'trainer' ? 'Buổi PT 1-kèm-1' : `Lớp ${r.className || 'nhóm'}`}
                    </div>
                  </div>
                  <Stars value={r.rating} size={14} />
                </div>
                {r.comment && <p className="muted mt8">“{r.comment}”</p>}
              </div>
            ))}
          </div>
        )
      ) : s.reviews.length === 0 ? (
        <div className="card"><Empty text="Chưa có đánh giá" /></div>
      ) : (
        <div className="grid g2">
          {s.reviews.map((r) => {
            const m = L.userById(db, r.memberId);
            return (
              <div className="card" key={r.id}>
                <div className="row">
                  <Avatar user={m} size="sm" />
                  <div className="grow">
                    <b>{m?.fullName}</b>
                    <div className="small dim">
                      {fmtDate(r.createdAt)} · {r.targetType === 'trainer' ? 'Buổi PT' : `Lớp ${L.classById(db, r.classId ?? '')?.name}`}
                    </div>
                  </div>
                  <Stars value={r.rating} size={14} />
                </div>
                {r.comment && <p className="muted mt8">“{r.comment}”</p>}
              </div>
            );
          })}
        </div>
      )}
    </>
  );
}

export default TrainerReviewsScreen;
