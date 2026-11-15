import React from 'react';
import { Star, MapPin, Award, Calendar, Phone, Mail } from 'lucide-react';
import type { TrainerItem } from '../services/trainer.service';
import { Badge } from './ui';

interface TrainerCardProps {
  trainer: TrainerItem;
  onSelect?: (trainer: TrainerItem) => void;
  showContact?: boolean;
}

export const TrainerCard: React.FC<TrainerCardProps> = ({ trainer, onSelect, showContact = false }) => {
  return (
    <div className="card hover col" style={{ padding: 18, justifyContent: 'space-between' }}>
      <div>
        <div className="row between mb12" style={{ alignItems: 'flex-start' }}>
          <div className="row" style={{ gap: 12 }}>
            <div
              style={{
                width: 50,
                height: 50,
                borderRadius: '50%',
                background: 'linear-gradient(135deg, var(--primary), #a78bfa)',
                color: '#fff',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: 20,
                fontWeight: 700,
              }}
            >
              {trainer.fullName.slice(0, 1).toUpperCase()}
            </div>
            <div>
              <div className="bold" style={{ fontSize: 16 }}>{trainer.fullName}</div>
              <div className="small muted row" style={{ gap: 4, marginTop: 2 }}>
                <MapPin size={13} /> {trainer.branchName || 'Chi nhánh OOAD'}
              </div>
            </div>
          </div>

          <div className="row" style={{ gap: 4, background: 'rgba(255,176,32,0.12)', padding: '4px 8px', borderRadius: 999 }}>
            <Star size={14} color="#f59e0b" fill="#f59e0b" />
            <span className="bold small" style={{ color: '#d97706' }}>{trainer.averageRating.toFixed(1)}</span>
            <span className="small dim">({trainer.reviewCount})</span>
          </div>
        </div>

        {trainer.specialty && (
          <div className="row wrap mb12" style={{ gap: 6 }}>
            {trainer.specialty.split(',').map((s, idx) => (
              <Badge key={idx} color="purple" plain>
                {s.trim()}
              </Badge>
            ))}
          </div>
        )}

        <div className="small muted row mb12" style={{ gap: 6 }}>
          <Award size={14} color="var(--primary)" />
          <span><b>{trainer.experienceYears}</b> năm kinh nghiệm huấn luyện</span>
        </div>

        {trainer.bio && (
          <p className="small muted" style={{ lineHeight: 1.5, maxHeight: 60, overflow: 'hidden', textOverflow: 'ellipsis' }}>
            {trainer.bio}
          </p>
        )}

        {showContact && (
          <div className="col mt12" style={{ gap: 4, borderTop: '1px solid var(--border)', paddingTop: 10 }}>
            {trainer.phone && (
              <div className="small row" style={{ gap: 6 }}>
                <Phone size={13} className="muted" /> <span>{trainer.phone}</span>
              </div>
            )}
            <div className="small row" style={{ gap: 6 }}>
              <Mail size={13} className="muted" /> <span>{trainer.email}</span>
            </div>
          </div>
        )}
      </div>

      {onSelect && (
        <button
          className="btn primary sm block mt16"
          onClick={() => onSelect(trainer)}
        >
          <Calendar size={14} /> Xem lịch & Đặt lịch PT
        </button>
      )}
    </div>
  );
};
