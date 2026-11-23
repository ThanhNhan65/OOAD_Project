import { useCallback, useEffect, useState } from 'react';
import {
  Search,
  CreditCard,
  CheckCircle2,
  Lock,
  Unlock,
  ShieldAlert,
  QrCode,
  Plus,
  RefreshCw,
} from 'lucide-react';
import { useApp } from '../../../store/AppContext';
import { Avatar, Badge, Empty, Modal, PageHead, StatCard, Tabs } from '../../../components/ui';
import { cardStatusMeta } from '../../../components/labels';
import { fmtDate } from '../../../utils';
import {
  MemberCardWidget,
  StaffIssueCardModal,
} from '../../../components/member-card';
import { memberCardService, type MemberCardDto } from '../../../api/memberCard.service';
import type { CardStatus } from '../../../types';

export function StaffMemberCardsScreen() {
  const { toast } = useApp();
  const [q, setQ] = useState('');
  const [statusFilter, setStatusFilter] = useState<'ALL' | CardStatus>('ALL');
  const [cards, setCards] = useState<MemberCardDto[]>([]);
  const [loading, setLoading] = useState<boolean>(true);
  const [issuingCard, setIssuingCard] = useState<MemberCardDto | null>(null);
  const [viewingCard, setViewingCard] = useState<MemberCardDto | null>(null);

  const loadCards = useCallback(async () => {
    setLoading(true);
    try {
      const list = await memberCardService.getMemberCards();
      setCards(list);
    } catch (err) {
      toast((err as Error).message, 'error');
    } finally {
      setLoading(false);
    }
  }, [toast]);

  useEffect(() => {
    loadCards();
  }, [loadCards]);

  // Mỗi hội viên lấy thẻ mới nhất (đã được sắp xếp từ backend) làm thẻ chính để hiển thị 1 dòng/hội viên
  const primaryCardsByMember = Array.from(
    cards.reduce((map, c) => {
      if (!map.has(c.memberId)) map.set(c.memberId, c);
      return map;
    }, new Map<string, MemberCardDto>()).values(),
  );

  const term = q.trim().toLowerCase();
  const filteredRows = primaryCardsByMember.filter((c) => {
    if (statusFilter !== 'ALL' && c.status !== statusFilter) return false;
    if (!term) return true;
    return (
      c.memberName.toLowerCase().includes(term) ||
      c.memberEmail.toLowerCase().includes(term) ||
      (c.memberPhone?.includes(term) ?? false) ||
      c.cardCode.toLowerCase().includes(term) ||
      (c.allCards?.some((sub) => sub.cardCode.toLowerCase().includes(term)) ?? false)
    );
  });

  const countByStatus = (st: CardStatus) =>
    primaryCardsByMember.filter((c) => c.status === st).length;

  const handleChangeStatus = async (
    card: MemberCardDto,
    newStatus: CardStatus,
    reason?: string,
  ) => {
    try {
      await memberCardService.updateCardStatus(card.id, {
        status: newStatus,
        reason,
      });
      toast(`Đã chuyển trạng thái thẻ ${card.cardCode} (${card.memberName}) sang ${newStatus}`, 'success');
      await loadCards();
    } catch (err) {
      toast((err as Error).message, 'error');
    }
  };

  return (
    <>
      <PageHead
        title="Quản lý Thẻ hội viên & QR Code"
        desc="Lễ tân tra cứu hội viên, cấp thẻ mới (mã ngẫu nhiên bảo mật), khóa/mở khóa thẻ hoặc xử lý báo mất thẻ."
        actions={
          <button className="btn sm" onClick={loadCards} disabled={loading}>
            <RefreshCw size={14} className={loading ? 'spin' : ''} /> Làm mới
          </button>
        }
      />

      {/* Thống kê nhanh */}
      <div className="grid g4 mb16">
        <StatCard
          icon={<CreditCard size={22} />}
          label="Tổng hội viên có thẻ"
          value={primaryCardsByMember.length}
          color="#7c5cff"
        />
        <StatCard
          icon={<CheckCircle2 size={22} />}
          label="Thẻ đang hoạt động (ACTIVE)"
          value={countByStatus('ACTIVE')}
          color="#2ed47a"
        />
        <StatCard
          icon={<Lock size={22} />}
          label="Thẻ đang tạm khóa (LOCKED)"
          value={countByStatus('LOCKED')}
          color="#ffb020"
        />
        <StatCard
          icon={<ShieldAlert size={22} />}
          label="Thẻ báo mất (LOST)"
          value={countByStatus('LOST')}
          color="#ff4d6d"
          sub="Cần cấp thẻ mới thay thế"
        />
      </div>

      {/* Bộ lọc & Tìm kiếm */}
      <div className="row between wrap mb16" style={{ gap: 12 }}>
        <Tabs
          value={statusFilter}
          onChange={setStatusFilter}
          tabs={[
            { id: 'ALL', label: 'Tất cả hội viên', count: primaryCardsByMember.length },
            { id: 'ACTIVE', label: 'Đang hoạt động (ACTIVE)', count: countByStatus('ACTIVE') },
            { id: 'LOCKED', label: 'Tạm khóa (LOCKED)', count: countByStatus('LOCKED') },
            { id: 'LOST', label: 'Báo mất (LOST)', count: countByStatus('LOST') },
          ]}
        />

        <div className="input-icon" style={{ minWidth: 320, maxWidth: 420, flex: 1 }}>
          <Search size={17} />
          <input
            className="input"
            placeholder="Tìm theo tên, SĐT, email hoặc mã thẻ (Card_Code)..."
            value={q}
            onChange={(e) => setQ(e.target.value)}
            id="staff-card-search"
          />
        </div>
      </div>

      {/* Bảng danh sách hội viên & thẻ */}
      <div className="table-wrap">
        <table>
          <thead>
            <tr>
              <th>Hội viên</th>
              <th>Mã thẻ hiện tại (Card_Code)</th>
              <th>Trạng thái thẻ</th>
              <th>Gói hội viên</th>
              <th>Lịch sử thẻ</th>
              <th style={{ textAlign: 'right' }}>Thao tác Lễ tân</th>
            </tr>
          </thead>
          <tbody>
            {filteredRows.map((c) => {
              const totalCardsCount = c.allCards?.length || 1;
              return (
                <tr key={c.id}>
                  <td>
                    <div className="row" style={{ gap: 10 }}>
                      <Avatar user={{ fullName: c.memberName, avatarColor: '#7c5cff' }} size="sm" />
                      <div>
                        <div className="row" style={{ gap: 6 }}>
                          <b>{c.memberName}</b>
                          {c.userStatus === 'LOCKED' && <Badge color="red">TK Khóa</Badge>}
                        </div>
                        <div className="small dim">
                          {c.memberPhone || '—'} · {c.memberEmail}
                        </div>
                      </div>
                    </div>
                  </td>

                  <td>
                    <span
                      style={{
                        fontFamily: 'monospace',
                        fontWeight: 700,
                        fontSize: 14,
                        letterSpacing: 1,
                        padding: '4px 8px',
                        borderRadius: 6,
                        background: 'var(--surface-2)',
                      }}
                    >
                      {c.cardCode}
                    </span>
                  </td>

                  <td>
                    <Badge color={cardStatusMeta[c.status].color}>
                      {c.status === 'ACTIVE' && 'ACTIVE · Hoạt động'}
                      {c.status === 'LOCKED' && 'LOCKED · Tạm khóa'}
                      {c.status === 'LOST' && 'LOST · Đã báo mất'}
                    </Badge>
                  </td>

                  <td>
                    {c.currentPlanName ? (
                      <div>
                        <div className="bold small">{c.currentPlanName}</div>
                        {c.planEndDate && <div className="small dim">HSD: {fmtDate(c.planEndDate.slice(0, 10))}</div>}
                      </div>
                    ) : (
                      <span className="small dim">Chưa có gói</span>
                    )}
                  </td>

                  <td>
                    <span className="small muted">{totalCardsCount} thẻ đã cấp</span>
                  </td>

                  <td style={{ textAlign: 'right' }}>
                    <div className="row wrap" style={{ justifyContent: 'flex-end', gap: 6 }}>
                      <button
                        type="button"
                        className="btn sm ghost"
                        onClick={() => setViewingCard(c)}
                        title="Xem mã QR & lịch sử thẻ"
                      >
                        <QrCode size={15} /> QR & Lịch sử
                      </button>

                      {c.status === 'ACTIVE' && (
                        <button
                          type="button"
                          className="btn sm"
                          onClick={() =>
                            handleChangeStatus(c, 'LOCKED', 'Lễ tân tạm khóa thẻ tại quầy')
                          }
                          title="Khóa tạm thời thẻ này"
                        >
                          <Lock size={14} /> Khóa thẻ
                        </button>
                      )}

                      {c.status === 'LOCKED' && (
                        <button
                          type="button"
                          className="btn sm success"
                          onClick={() =>
                            handleChangeStatus(c, 'ACTIVE', 'Lễ tân mở khóa thẻ tại quầy')
                          }
                          title="Mở khóa lại thẻ này"
                        >
                          <Unlock size={14} /> Mở khóa
                        </button>
                      )}

                      {c.status !== 'LOST' && (
                        <button
                          type="button"
                          className="btn sm danger"
                          onClick={() =>
                            handleChangeStatus(c, 'LOST', 'Lễ tân ghi nhận hội viên báo mất thẻ')
                          }
                          title="Đánh dấu thẻ bị mất (Vô hiệu hóa vĩnh viễn)"
                        >
                          <ShieldAlert size={14} /> Báo mất
                        </button>
                      )}

                      <button
                        type="button"
                        className="btn sm primary"
                        onClick={() => setIssuingCard(c)}
                        id={`issue-card-${c.memberId}`}
                        title="Cấp thẻ mới (Thẻ cũ tự chuyển sang LOCKED/LOST)"
                      >
                        <Plus size={14} /> Cấp thẻ mới
                      </button>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
        {!loading && filteredRows.length === 0 && (
          <Empty
            text="Không tìm thấy hội viên hoặc thẻ phù hợp với bộ lọc"
            icon={<CreditCard size={40} />}
          />
        )}
      </div>

      {/* Modal cấp thẻ mới */}
      {issuingCard && (
        <StaffIssueCardModal
          card={issuingCard}
          onIssued={loadCards}
          onClose={() => setIssuingCard(null)}
        />
      )}

      {/* Modal xem chi tiết thẻ, QR Code & Lịch sử thẻ */}
      {viewingCard && (
        <Modal
          title={`Chi tiết Thẻ hội viên & QR Code · ${viewingCard.memberName}`}
          onClose={() => setViewingCard(null)}
          wide
          footer={
            <div className="row between" style={{ width: '100%' }}>
              <button
                className="btn primary"
                onClick={() => {
                  const target = viewingCard;
                  setViewingCard(null);
                  setIssuingCard(target);
                }}
              >
                <Plus size={15} /> Cấp đổi thẻ mới cho hội viên này
              </button>
              <button className="btn" onClick={() => setViewingCard(null)}>
                Đóng
              </button>
            </div>
          }
        >
          <MemberCardWidget cardData={viewingCard} showHistory />
        </Modal>
      )}
    </>
  );
}

export default StaffMemberCardsScreen;
