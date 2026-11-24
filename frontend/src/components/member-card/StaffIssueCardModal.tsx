import { useState } from 'react';
import { CreditCard, Sparkles, ShieldAlert, Lock, CheckCircle2 } from 'lucide-react';
import { Avatar, Badge, Modal } from '../ui';
import { useApp } from '../../store/AppContext';
import { memberCardService, type MemberCardDto } from '../../api/memberCard.service';
import { QRCodeDisplay } from './QRCodeDisplay';
import { cardStatusMeta } from '../labels';

export interface StaffIssueCardModalProps {
  card: MemberCardDto;
  onClose: () => void;
  onIssued?: (newCardCode: string) => void | Promise<void>;
}

export function StaffIssueCardModal({
  card,
  onClose,
  onIssued,
}: StaffIssueCardModalProps) {
  const { toast } = useApp();
  const [previousCardStatus, setPreviousCardStatus] = useState<'LOST' | 'LOCKED'>(
    card.status === 'LOST' ? 'LOST' : 'LOCKED',
  );
  const [reason, setReason] = useState(
    card.status === 'LOST'
      ? 'Cấp lại thẻ mới thay thế thẻ hội viên đã báo mất'
      : 'Cấp đổi thẻ hội viên mới tại quầy lễ tân',
  );
  const [loading, setLoading] = useState(false);
  const [issuedCode, setIssuedCode] = useState<string | null>(null);

  const handleIssue = async () => {
    setLoading(true);
    try {
      const issued = await memberCardService.issueCard({
        memberId: card.memberId,
        previousCardStatus,
        reason: reason.trim(),
      });
      toast(`Đã cấp thẻ mới ${issued.cardCode} cho hội viên ${card.memberName}`, 'success');
      setIssuedCode(issued.cardCode);
      await onIssued?.(issued.cardCode);
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Modal
      title={issuedCode ? 'Cấp thẻ hội viên mới thành công' : `Cấp thẻ hội viên mới · ${card.memberName}`}
      onClose={onClose}
      footer={
        issuedCode ? (
          <button className="btn primary" onClick={onClose} id="issue-card-done-btn">
            <CheckCircle2 size={16} /> Hoàn tất
          </button>
        ) : (
          <>
            <button className="btn" onClick={onClose} disabled={loading}>
              Hủy
            </button>
            <button
              className="btn primary"
              onClick={handleIssue}
              disabled={loading}
              id="confirm-issue-card-btn"
            >
              <CreditCard size={16} /> {loading ? 'Đang sinh mã thẻ...' : 'Cấp thẻ mới (Sinh mã ngẫu nhiên)'}
            </button>
          </>
        )
      }
    >
      {issuedCode ? (
        <div className="col" style={{ gap: 16, alignItems: 'center', textAlign: 'center', padding: '8px 0' }}>
          <Badge color="green">Đã kích hoạt thẻ mới (ACTIVE)</Badge>
          <div>
            <div className="small muted">Mã thẻ mới (Ngẫu nhiên, duy nhất, không đoán được)</div>
            <div
              style={{
                fontFamily: 'monospace',
                fontSize: 24,
                fontWeight: 800,
                letterSpacing: 3,
                color: 'var(--primary)',
                marginTop: 4,
              }}
            >
              {issuedCode}
            </div>
          </div>
          <QRCodeDisplay value={issuedCode} size={156} status="ACTIVE" />
          <p className="small muted" style={{ maxWidth: 400 }}>
            Thẻ cũ của hội viên <b>{card.memberName}</b> đã tự động chuyển sang trạng thái{' '}
            <b>{previousCardStatus}</b>. Hội viên có thể sử dụng ngay mã QR mới này tại trang Hồ sơ cá nhân.
          </p>
        </div>
      ) : (
        <div className="col" style={{ gap: 16 }}>
          {/* Thông tin hội viên */}
          <div className="card" style={{ background: 'var(--surface-2)', padding: 14 }}>
            <div className="row between wrap" style={{ gap: 10 }}>
              <div className="row" style={{ gap: 10 }}>
                <Avatar user={{ fullName: card.memberName, avatarColor: '#7c5cff' }} />
                <div>
                  <div className="bold" style={{ fontSize: 15 }}>{card.memberName}</div>
                  <div className="small muted">{card.memberPhone || '—'} · {card.memberEmail}</div>
                </div>
              </div>
              {card.cardCode && (
                <div style={{ textAlign: 'right' }}>
                  <div className="small muted">Thẻ hiện tại</div>
                  <div className="row" style={{ gap: 6, marginTop: 2 }}>
                    <span style={{ fontFamily: 'monospace', fontWeight: 700 }}>{card.cardCode}</span>
                    <Badge color={cardStatusMeta[card.status].color}>{card.status}</Badge>
                  </div>
                </div>
              )}
            </div>
          </div>

          {/* Chọn xử lý cho thẻ cũ */}
          {card.status !== 'LOST' && (
            <div className="field">
              <label>Xử lý đối với thẻ cũ ({card.cardCode}) khi cấp thẻ mới</label>
              <div className="grid g2" style={{ gap: 10, marginTop: 6 }}>
                <button
                  type="button"
                  className={`slot-btn ${previousCardStatus === 'LOCKED' ? 'on' : ''}`}
                  onClick={() => {
                    setPreviousCardStatus('LOCKED');
                    setReason('Cấp đổi thẻ hội viên mới tại quầy lễ tân');
                  }}
                  style={{ textAlign: 'left', padding: 12 }}
                >
                  <div className="row" style={{ gap: 6, fontWeight: 700 }}>
                    <Lock size={15} color="#ffb020" /> Chuyển thẻ cũ sang LOCKED
                  </div>
                  <div className="small muted mt4">
                    Dùng khi đổi thẻ mới hoặc thẻ cũ bị hỏng/mờ mã.
                  </div>
                </button>

                <button
                  type="button"
                  className={`slot-btn ${previousCardStatus === 'LOST' ? 'on' : ''}`}
                  onClick={() => {
                    setPreviousCardStatus('LOST');
                    setReason('Cấp lại thẻ mới do hội viên báo mất thẻ cũ');
                  }}
                  style={{ textAlign: 'left', padding: 12 }}
                >
                  <div className="row" style={{ gap: 6, fontWeight: 700 }}>
                    <ShieldAlert size={15} color="#ff4d6d" /> Chuyển thẻ cũ sang LOST
                  </div>
                  <div className="small muted mt4">
                    Dùng khi hội viên báo mất thẻ (vô hiệu hóa vĩnh viễn thẻ cũ).
                  </div>
                </button>
              </div>
            </div>
          )}

          <div className="field">
            <label>Lý do cấp thẻ / Ghi chú</label>
            <input
              className="input"
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              placeholder="Nhập lý do cấp thẻ mới..."
            />
          </div>

          <div
            className="card"
            style={{
              background: 'rgba(124, 92, 255, 0.08)',
              borderColor: 'rgba(124, 92, 255, 0.3)',
              padding: 12,
            }}
          >
            <div className="row" style={{ gap: 8 }}>
              <Sparkles size={16} color="var(--primary)" />
              <span className="small">
                Hệ thống sẽ tự động sinh mã <b>Card_Code</b> ngẫu nhiên, duy nhất, không thể đoán trước (định dạng{' '}
                <code>DF-XXXX-XXXX</code>) và gắn trực tiếp với <code>Member_Id</code>.
              </span>
            </div>
          </div>
        </div>
      )}
    </Modal>
  );
}

export default StaffIssueCardModal;
