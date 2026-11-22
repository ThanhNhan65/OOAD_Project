import { useCallback, useEffect, useState } from 'react';
import { Wifi, Copy, Check, CreditCard, ShieldAlert, Lock, CheckCircle2, History } from 'lucide-react';
import { Badge } from '../ui';
import { cardStatusMeta } from '../labels';
import { fmtDate } from '../../utils';
import { useApp } from '../../store/AppContext';
import { memberCardService, type MemberCardDto } from '../../api/memberCard.service';
import { getAuthToken } from '../../api/apiClient';
import { QRCodeDisplay } from './QRCodeDisplay';
import { ReportLostCardButton } from './ReportLostCardButton';
import type { CardStatus } from '../../types';

export interface MemberCardWidgetProps {
  memberId?: string;
  cardData?: MemberCardDto;
  compact?: boolean;
  showHistory?: boolean;
  onCardChanged?: () => void | Promise<void>;
}

const mapMembershipStatusBadge = (status?: string | null): [string, string] => {
  switch (status?.toLowerCase()) {
    case 'active':
      return ['Đang hiệu lực', 'green'];
    case 'frozen':
      return ['Đang tạm ngưng', 'purple'];
    case 'pending':
      return ['Chờ thanh toán', 'amber'];
    case 'cancelled':
      return ['Đã hủy', 'red'];
    default:
      return ['Hết hạn', 'gray'];
  }
};

export function MemberCardWidget({
  memberId,
  cardData,
  compact = false,
  showHistory = true,
  onCardChanged,
}: MemberCardWidgetProps) {
  const { user } = useApp();
  const [fetchedCard, setFetchedCard] = useState<MemberCardDto | null>(cardData ?? null);
  const [loading, setLoading] = useState<boolean>(!cardData && !!getAuthToken());
  const [copied, setCopied] = useState(false);

  const loadCard = useCallback(async () => {
    if (cardData) {
      setFetchedCard(cardData);
      setLoading(false);
      return;
    }
    if (!getAuthToken() || !user) {
      setLoading(false);
      return;
    }
    setLoading(true);
    try {
      const isSelf = !memberId || memberId === user.id || memberId === user.memberId;
      if (isSelf && user.role === 'member') {
        const myCard = await memberCardService.getMyCard();
        setFetchedCard(myCard);
      } else if (memberId) {
        const cards = await memberCardService.getMemberCards({ memberId });
        setFetchedCard(cards[0] ?? null);
      }
    } catch {
      /* keep current fetchedCard or null */
    } finally {
      setLoading(false);
    }
  }, [cardData, memberId, user]);

  useEffect(() => {
    loadCard();
  }, [loadCard]);

  if (loading && !fetchedCard) {
    return <div className="card" style={{ padding: 20 }}>Đang tải thông tin thẻ hội viên...</div>;
  }

  const displayCard = cardData ?? fetchedCard;
  if (!displayCard) return null;

  const cardCode = displayCard.cardCode || 'DF-NONE';
  const cardStatus: CardStatus = displayCard.status || 'ACTIVE';
  const cardId = displayCard.id;
  const isOwner = !!user && (user.id === displayCard.userId || user.memberId === displayCard.memberId);
  const [mLabel, mColor] = mapMembershipStatusBadge(displayCard.membershipStatus);
  const historyCards = displayCard.allCards ?? [];

  const copyCardCode = () => {
    navigator.clipboard.writeText(cardCode);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <div className="col" style={{ gap: 16 }}>
      <div
        className="member-card"
        style={{
          background:
            cardStatus === 'LOST'
              ? 'linear-gradient(135deg, #450a0a 0%, #7f1d1d 55%, #991b1b 100%)'
              : cardStatus === 'LOCKED'
              ? 'linear-gradient(135deg, #422006 0%, #78350f 55%, #92400e 100%)'
              : undefined,
        }}
      >
        <div style={{ position: 'relative' }}>
          <div className="row between">
            <div className="row" style={{ gap: 8 }}>
              <CreditCard size={20} style={{ opacity: 0.9 }} />
              <div className="brand-name">
                DFGym<span style={{ color: '#ff8fb0' }}>Center</span>
              </div>
            </div>
            <div className="row" style={{ gap: 10 }}>
              <Badge color={cardStatusMeta[cardStatus].color}>
                {cardStatus === 'ACTIVE' && <CheckCircle2 size={12} style={{ marginRight: 4 }} />}
                {cardStatus === 'LOCKED' && <Lock size={12} style={{ marginRight: 4 }} />}
                {cardStatus === 'LOST' && <ShieldAlert size={12} style={{ marginRight: 4 }} />}
                {cardStatusMeta[cardStatus].label}
              </Badge>
              <Wifi size={20} style={{ transform: 'rotate(90deg)', opacity: 0.7 }} />
            </div>
          </div>

          <div
            className="row between mt24 wrap"
            style={{ alignItems: 'flex-end', gap: 16 }}
          >
            <div>
              <div className="small" style={{ opacity: 0.75 }}>
                Chủ thẻ hội viên
              </div>
              <div style={{ fontSize: compact ? 19 : 22, fontWeight: 800 }}>
                {displayCard.memberName}
              </div>

              <div className="row" style={{ gap: 8, marginTop: 8 }}>
                <div
                  style={{
                    fontFamily: 'monospace',
                    fontSize: compact ? 16 : 19,
                    letterSpacing: 2.5,
                    fontWeight: 700,
                    background: 'rgba(255,255,255,0.14)',
                    padding: '4px 10px',
                    borderRadius: 8,
                  }}
                >
                  {cardCode}
                </div>
                <button
                  type="button"
                  onClick={copyCardCode}
                  title="Sao chép mã thẻ"
                  style={{
                    background: 'rgba(255,255,255,0.16)',
                    border: 'none',
                    color: '#fff',
                    borderRadius: 8,
                    padding: '6px 9px',
                    cursor: 'pointer',
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 4,
                    fontSize: 12,
                  }}
                >
                  {copied ? <Check size={14} /> : <Copy size={14} />}
                  {copied ? 'Đã chép' : 'Copy'}
                </button>
              </div>

              <div className="row mt16 wrap" style={{ gap: 8 }}>
                {displayCard.currentPlanName ? (
                  <Badge color="pink">{displayCard.currentPlanName}</Badge>
                ) : (
                  <Badge color="gray">Chưa đăng ký gói</Badge>
                )}
                {displayCard.currentPlanName && <Badge color={mColor}>{mLabel}</Badge>}
              </div>
              {displayCard.planStartDate && displayCard.planEndDate && (
                <div className="small mt8" style={{ opacity: 0.8 }}>
                  Hiệu lực gói: {fmtDate(displayCard.planStartDate.slice(0, 10))} → {fmtDate(displayCard.planEndDate.slice(0, 10))}
                </div>
              )}
            </div>

            <QRCodeDisplay
              value={cardCode}
              size={compact ? 112 : 128}
              status={cardStatus}
            />
          </div>
        </div>
      </div>

      {/* Thanh trạng thái & Nút Báo mất thẻ */}
      <div className="card" style={{ padding: '14px 16px' }}>
        <div className="row between wrap" style={{ gap: 12 }}>
          <div className="row" style={{ gap: 10 }}>
            {cardStatus === 'ACTIVE' && <CheckCircle2 size={20} color="#2ed47a" />}
            {cardStatus === 'LOCKED' && <Lock size={20} color="#ffb020" />}
            {cardStatus === 'LOST' && <ShieldAlert size={20} color="#ff4d6d" />}
            <div>
              <div className="bold" style={{ fontSize: 14 }}>
                Trạng thái thẻ: {cardStatusMeta[cardStatus].label}
              </div>
              <div className="small muted">{cardStatusMeta[cardStatus].desc}</div>
            </div>
          </div>

          {isOwner && cardStatus !== 'LOST' && (
            <ReportLostCardButton
              cardId={cardId}
              cardCode={cardCode}
              status={cardStatus}
              onReported={async () => {
                await loadCard();
                await onCardChanged?.();
              }}
            />
          )}
        </div>
      </div>

      {/* Lịch sử các thẻ đã cấp của hội viên */}
      {showHistory && historyCards.length > 1 && (
        <div className="card" style={{ padding: '14px 16px' }}>
          <div className="row between mb12">
            <h3 style={{ fontSize: 14.5, display: 'flex', alignItems: 'center', gap: 6 }}>
              <History size={16} /> Lịch sử cấp / đổi thẻ hội viên ({historyCards.length})
            </h3>
            <span className="small muted">Thẻ cũ tự động chuyển LOCKED / LOST khi cấp thẻ mới</span>
          </div>
          <div className="col" style={{ gap: 8 }}>
            {historyCards.map((c, idx) => (
              <div
                key={c.id}
                className="row between wrap"
                style={{
                  padding: '8px 12px',
                  borderRadius: 10,
                  background: idx === 0 ? 'var(--primary-soft)' : 'var(--surface-2)',
                  border: idx === 0 ? '1px solid var(--primary)' : '1px solid var(--border)',
                }}
              >
                <div className="row" style={{ gap: 10 }}>
                  <span style={{ fontFamily: 'monospace', fontWeight: 700, fontSize: 13.5 }}>
                    {c.cardCode}
                  </span>
                  {idx === 0 && <Badge color="purple" plain>Thẻ hiện tại</Badge>}
                </div>
                <div className="row" style={{ gap: 10 }}>
                  <Badge color={cardStatusMeta[c.status].color}>{c.status}</Badge>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

export default MemberCardWidget;
