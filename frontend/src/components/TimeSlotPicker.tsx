import React, { useState } from 'react';
import { Plus, Clock } from 'lucide-react';

interface TimeSlotPickerProps {
  onAddSlot: (startAt: string, endAt: string) => Promise<void>;
  loading?: boolean;
}

const DEFAULT_HOURS = [
  { label: '06:00 - 07:00', start: '06:00', end: '07:00' },
  { label: '07:00 - 08:00', start: '07:00', end: '08:00' },
  { label: '08:00 - 09:00', start: '08:00', end: '09:00' },
  { label: '09:00 - 10:00', start: '09:00', end: '10:00' },
  { label: '10:00 - 11:00', start: '10:00', end: '11:00' },
  { label: '14:00 - 15:00', start: '14:00', end: '15:00' },
  { label: '15:00 - 16:00', start: '15:00', end: '16:00' },
  { label: '16:00 - 17:00', start: '16:00', end: '17:00' },
  { label: '17:00 - 18:00', start: '17:00', end: '18:00' },
  { label: '18:00 - 19:00', start: '18:00', end: '19:00' },
  { label: '19:00 - 20:00', start: '19:00', end: '20:00' },
  { label: '20:00 - 21:00', start: '20:00', end: '21:00' },
];

export const TimeSlotPicker: React.FC<TimeSlotPickerProps> = ({ onAddSlot, loading = false }) => {
  const tomorrow = new Date();
  tomorrow.setDate(tomorrow.getDate() + 1);
  const tomorrowStr = tomorrow.toISOString().slice(0, 10);

  const [date, setDate] = useState(tomorrowStr);
  const [selectedSlotIndex, setSelectedSlotIndex] = useState(0);

  const handleAdd = async () => {
    const slot = DEFAULT_HOURS[selectedSlotIndex];
    const startAt = `${date}T${slot.start}:00Z`;
    const endAt = `${date}T${slot.end}:00Z`;
    await onAddSlot(startAt, endAt);
  };

  return (
    <div className="card" style={{ padding: 18 }}>
      <div className="row between mb12">
        <h3 className="row" style={{ gap: 8, fontSize: 16 }}>
          <Clock size={18} color="var(--primary)" /> Mở thêm khung giờ rảnh mới
        </h3>
      </div>

      <div className="grid g3 mb12" style={{ gap: 12 }}>
        <div className="field">
          <label className="small">Chọn ngày mở lịch</label>
          <input
            type="date"
            className="input sm"
            value={date}
            min={new Date().toISOString().slice(0, 10)}
            onChange={(e) => setDate(e.target.value)}
          />
        </div>

        <div className="field" style={{ gridColumn: 'span 2' }}>
          <label className="small">Chọn ca giờ (1 tiếng)</label>
          <select
            className="input sm"
            value={selectedSlotIndex}
            onChange={(e) => setSelectedSlotIndex(Number(e.target.value))}
          >
            {DEFAULT_HOURS.map((h, i) => (
              <option key={i} value={i}>
                {h.label}
              </option>
            ))}
          </select>
        </div>
      </div>

      <button className="btn primary sm" onClick={handleAdd} disabled={loading} style={{ gap: 6 }}>
        <Plus size={15} /> {loading ? 'Đang thêm...' : 'Tạo khung giờ rảnh'}
      </button>
    </div>
  );
};
