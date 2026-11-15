import React from 'react';
import { Trash2, CheckCircle2, Lock } from 'lucide-react';
import type { TrainerAvailabilityItem } from '../services/trainer.service';
import { Badge } from './ui';

interface AvailabilityGridProps {
  availabilities: TrainerAvailabilityItem[];
  onDeleteSlot?: (id: string) => Promise<void>;
  onBookSlot?: (slot: TrainerAvailabilityItem) => void;
  isOwner?: boolean;
}

export const AvailabilityGrid: React.FC<AvailabilityGridProps> = ({
  availabilities,
  onDeleteSlot,
  onBookSlot,
  isOwner = false,
}) => {
  if (availabilities.length === 0) {
    return (
      <div className="card text-center" style={{ padding: 32 }}>
        <p className="muted">Chưa có khung giờ rảnh nào được tạo trong thời gian này.</p>
      </div>
    );
  }

  // Nhóm theo ngày
  const grouped = availabilities.reduce<Record<string, TrainerAvailabilityItem[]>>((acc, curr) => {
    const day = curr.startAt.slice(0, 10);
    if (!acc[day]) acc[day] = [];
    acc[day].push(curr);
    return acc;
  }, {});

  const formatDayName = (dateStr: string) => {
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit' });
    } catch {
      return dateStr;
    }
  };

  const formatTime = (iso: string) => {
    try {
      const d = new Date(iso);
      return d.toLocaleTimeString('vi-VN', { hour: '2-digit', minute: '2-digit', hour12: false });
    } catch {
      return iso;
    }
  };

  return (
    <div className="col" style={{ gap: 16 }}>
      {Object.entries(grouped).map(([day, slots]) => (
        <div key={day} className="card" style={{ padding: 16 }}>
          <div className="bold mb12" style={{ fontSize: 15, color: 'var(--primary)', textTransform: 'capitalize' }}>
            📅 {formatDayName(day)}
          </div>

          <div className="row wrap" style={{ gap: 10 }}>
            {slots.map((s) => {
              const timeRange = `${formatTime(s.startAt)} - ${formatTime(s.endAt)}`;

              return (
                <div
                  key={s.id}
                  style={{
                    padding: '8px 12px',
                    borderRadius: 8,
                    background: s.isBooked ? 'rgba(255, 77, 109, 0.08)' : 'var(--surface-2)',
                    border: `1px solid ${s.isBooked ? 'rgba(255, 77, 109, 0.3)' : 'var(--border)'}`,
                    display: 'flex',
                    alignItems: 'center',
                    gap: 8,
                  }}
                >
                  <span className="bold" style={{ fontSize: 13 }}>{timeRange}</span>

                  {s.isBooked ? (
                    <Badge color="red" plain>
                      <Lock size={12} style={{ marginRight: 2 }} /> Đã đặt
                    </Badge>
                  ) : (
                    <Badge color="green" plain>
                      <CheckCircle2 size={12} style={{ marginRight: 2 }} /> Sẵn sàng
                    </Badge>
                  )}

                  {isOwner && !s.isBooked && onDeleteSlot && (
                    <button
                      className="icon-btn text-danger"
                      title="Xóa ca rảnh"
                      onClick={() => onDeleteSlot(s.id)}
                      style={{ padding: 4, marginLeft: 4 }}
                    >
                      <Trash2 size={14} />
                    </button>
                  )}

                  {!isOwner && !s.isBooked && onBookSlot && (
                    <button
                      className="btn primary sm"
                      style={{ padding: '3px 8px', fontSize: 12, marginLeft: 6 }}
                      onClick={() => onBookSlot(s)}
                    >
                      Đặt ca
                    </button>
                  )}
                </div>
              );
            })}
          </div>
        </div>
      ))}
    </div>
  );
};
